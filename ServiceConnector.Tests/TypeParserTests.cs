using ServiceConnector.Common;
using ServiceConnector.Jobs;

namespace ServiceConnector.Tests;

public class TypeParserTests
{
	private readonly TypeFinder _typeFinder = new();

	private readonly TypesStore _types = new()
	{
		["dict"] = typeof(Dictionary<int, int>),
		["dict2"] = typeof(Dictionary<string, TestObject>),
		["list"] = typeof(List<int>),
		["arr"] = typeof(int[]),
		["IArray"] = typeof(TestArray),
	};

	[Theory]
	[InlineData(null, typeof(string))]
	[InlineData("", typeof(string))]
	[InlineData("    ", typeof(string))]
	[InlineData("dict", typeof(string))]
	[InlineData("$dict", typeof(Dictionary<int, int>))]
	[InlineData("$dict.0", typeof(int))]
	[InlineData("$dict.10", typeof(int))]
	[InlineData("$dict2.0", typeof(TestObject))]
	[InlineData("$dict2.0.item0", typeof(int))]
	[InlineData("$dict2.0.item1", typeof(string))]
	[InlineData("$list", typeof(List<int>))]
	[InlineData("$list.0", typeof(int))]
	[InlineData("$arr", typeof(int[]))]
	[InlineData("$arr.0", typeof(int))]
	[InlineData("$iArray", typeof(TestArray))]
	[InlineData("$IArray.0", typeof(int))]
	[InlineData("$IArray.1", typeof(string))]
	[InlineData("$IArray.2", typeof(TestObject))]
	[InlineData("$IArray.2.Item1", typeof(string))]
	[InlineData("$IArray.2.SomeField", typeof(string))]
	public void SuccessTests(string path, Type expectedType)
	{
		Assert.Equal(expectedType, _typeFinder.ParseType(path, _types));
	}

	[Theory]
	[InlineData("$some", "Type some doesn't exist")]
	[InlineData("$IArray.2.Item2", "Type Item2 in IArray.2 doesn't exist")]
	[InlineData("$IArray.2.Item2.Some", "Type Item2 in IArray.2 doesn't exist")]
	public void FailTests(string path, string error)
	{
		try
		{
			_typeFinder.ParseType(path, _types);
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
		public string Item_1 { get; set; }
		public TestObject Item_2 { get; set; }
	}

	private class TestObject
	{
		public int Item0 { get; set; }
		public string Item1 { get; set; }
		public string SomeField;
	}
}