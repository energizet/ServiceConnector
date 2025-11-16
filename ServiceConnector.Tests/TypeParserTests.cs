using System.Collections;
using System.Linq.Expressions;
using ServiceConnector.Common;
using ServiceConnector.Jobs;

namespace ServiceConnector.Tests;

public class TypeParserTests
{
	private readonly ExpressionGeneratorFactory _generatorFactory = new();
	private readonly TypeFinder _typeFinder = new(new LinkerTest());

	private static readonly TypesStore Types = new()
	{
		["dict"] = typeof(Dictionary<int, int>),
		["dict2"] = typeof(Dictionary<string, TestObject>),
		["list"] = typeof(List<int>),
		["arr"] = typeof(int[]),
		["IArray"] = typeof(TestArray),
	};

	private static readonly PipelineStore Store = new()
	{
		["dict"] = new Dictionary<int, int>
		{
			[0] = 11,
			[1] = 12,
		},
		["dict2"] = new Dictionary<string, TestObject>
		{
			["0"] = new()
			{
				Item0 = 21,
				Item1 = "22",
				SomeField = "23",
			},
		},
		["list"] = new List<int>
		{
			3,
		},
		["arr"] = new int[]
		{
			4,
		},
		["IArray"] = new TestArray
		{
			Item_0 = 51,
			Item_1 = "52",
			Item_2 = new()
			{
				Item0 = 53,
				Item1 = "54",
				SomeField = "55",
			},
		},
	};

	[Theory]
	[MemberData(nameof(SuccessCases))]
	public void SuccessTests(string path, Type expectedType, object? expectedValue)
	{
		Assert.Equal(expectedType, _typeFinder.ParseType(path, Types));

		var store = Expression.Parameter(typeof(PipelineStore), "store");
		var lambda = _generatorFactory.Create().GetValue(path, Types);
		var invoke =  Expression.Invoke(lambda, store);
		var func = Expression.Lambda<Func<PipelineStore, object?>>(Expression.Convert(invoke, typeof(object)), store).Compile();

		Assert.Equal(expectedValue, func(Store));
	}

	public static IEnumerable<object?[]> SuccessCases()
	{
		yield return ["", typeof(string), ""];
		yield return ["    ", typeof(string), "    "];
		yield return ["dict", typeof(string), "dict"];
		
		yield return ["$dict", typeof(Dictionary<int, int>), Store.Get<Dictionary<int, int>>("dict")!];
		yield return ["$dict.0", typeof(int?), Store.Get<Dictionary<int, int>>("dict")![0]];
		yield return ["$dict.1", typeof(int?), Store.Get<Dictionary<int, int>>("dict")![1]];
		yield return ["$dict.2", typeof(int?), null];
		
		yield return ["$dict2.0", typeof(TestObject), Store.Get<Dictionary<string, TestObject>>("dict2")!["0"]];
		yield return ["$dict2.0.item0", typeof(int?), Store.Get<Dictionary<string, TestObject>>("dict2")!["0"].Item0];
		yield return ["$dict2.0.item1", typeof(string), Store.Get<Dictionary<string, TestObject>>("dict2")!["0"].Item1];
		yield return ["$dict2.1.item0", typeof(int?), null];
		
		yield return ["$list", typeof(List<int>), Store.Get<List<int>>("list")!];
		yield return ["$list.0", typeof(int?), Store.Get<List<int>>("list")![0]];
		yield return ["$list.1", typeof(int?), null];
		
		yield return ["$arr", typeof(int[]), Store.Get<int[]>("arr")!];
		yield return ["$arr.0", typeof(int?), Store.Get<int[]>("arr")![0]];
		yield return ["$arr.1", typeof(int?), null];
		
		yield return ["$iArray", typeof(TestArray), Store.Get<TestArray>("IArray")!];
		yield return ["$IArray.0", typeof(int?), Store.Get<TestArray>("IArray")!.Item_0];
		yield return ["$IArray.1", typeof(string), Store.Get<TestArray>("IArray")!.Item_1];
		yield return ["$IArray.2", typeof(TestObject), Store.Get<TestArray>("IArray")!.Item_2];
		yield return ["$IArray.2.Item1", typeof(string), Store.Get<TestArray>("IArray")!.Item_2.Item1];
		yield return ["$IArray.2.SomeField", typeof(string), Store.Get<TestArray>("IArray")!.Item_2.SomeField];
	}

	[Theory]
	[InlineData("$some", "Type some doesn't exist")]
	[InlineData("$IArray.2.Item2", "Type Item2 in IArray.2 doesn't exist")]
	[InlineData("$IArray.2.Item2.Some", "Type Item2 in IArray.2 doesn't exist")]
	public void FailTests(string path, string error)
	{
		try
		{
			_typeFinder.ParseType(path, Types);
		}
		catch (ArgumentException ex)
		{
			Assert.Equal(error, ex.Message);
			return;
		}

		Assert.Fail();
	}

	private class TestArray : IArray
	{
		public int Item_0 { get; set; }
		public string Item_1 { get; set; } = null!;
		public TestObject Item_2 { get; set; } = null!;

		public IEnumerator GetEnumerator()
		{
			yield return Item_0;
			yield return Item_1;
			yield return Item_2;
		}
	}

	private class TestObject
	{
		public int Item0 { get; set; }
		public string Item1 { get; set; } = null!;
		public string SomeField = null!;
	}

	private class LinkerTest : ILinker
	{
		public void Link(string from)
		{
		}
	}
}