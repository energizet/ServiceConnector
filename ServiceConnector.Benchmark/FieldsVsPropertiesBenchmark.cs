using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;




[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[RankColumn]
public class FieldPropertyBenchmark2
{
	private FieldValueModel _fieldItems = new()
	{
		Value = 10,
	};
	private AutoPropertyValueModel _autoPropertyItems = new()
	{
		Value = 10,
	};
	private ManualPropertyValueModel _manualPropertyItems = new()
	{
		Value = 10,
	};

	[Benchmark(Baseline = true, Description = "Field read")]
	[BenchmarkCategory("Read")]
	public int ReadField()
	{
		return _fieldItems.Value;
	}

	[Benchmark(Description = "Auto property read")]
	[BenchmarkCategory("Read")]
	public int ReadAutoProperty()
	{
		return _autoPropertyItems.Value;
	}

	[Benchmark(Description = "Manual property read")]
	[BenchmarkCategory("Read")]
	public int ReadManualProperty()
	{
		return _manualPropertyItems.Value;
	}

	[Benchmark(Baseline = true, Description = "Field write")]
	[BenchmarkCategory("Write")]
	public int WriteField()
	{
		_fieldItems.Value = 20;
		return _fieldItems.Value;
	}

	[Benchmark(Description = "Auto property write")]
	[BenchmarkCategory("Write")]
	public int WriteAutoProperty()
	{
		_autoPropertyItems.Value = 20;
		return _autoPropertyItems.Value;
	}

	[Benchmark(Description = "Manual property write")]
	[BenchmarkCategory("Write")]
	public int WriteManualProperty()
	{
		_manualPropertyItems.Value = 20;
		return _manualPropertyItems.Value;
	}
}


[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[RankColumn]
public class FieldPropertyBenchmark
{
	private const int ItemsCount = 1024;

	private FieldValueModel[] _fieldItems = [];
	private AutoPropertyValueModel[] _autoPropertyItems = [];
	private ManualPropertyValueModel[] _manualPropertyItems = [];

	private int _writeSeed;

	[GlobalSetup]
	public void Setup()
	{
		_fieldItems = new FieldValueModel[ItemsCount];
		_autoPropertyItems = new AutoPropertyValueModel[ItemsCount];
		_manualPropertyItems = new ManualPropertyValueModel[ItemsCount];

		for (var i = 0; i < ItemsCount; i++)
		{
			_fieldItems[i] = new FieldValueModel { Value = i };
			_autoPropertyItems[i] = new AutoPropertyValueModel { Value = i };
			_manualPropertyItems[i] = new ManualPropertyValueModel { Value = i };
		}
	}

	[Benchmark(Baseline = true, Description = "Field read", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Read")]
	public int ReadField()
	{
		var sum = 0;
		var items = _fieldItems;

		for (var i = 0; i < items.Length; i++)
		{
			sum += items[i].Value;
		}

		return sum;
	}

	[Benchmark(Description = "Auto property read", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Read")]
	public int ReadAutoProperty()
	{
		var sum = 0;
		var items = _autoPropertyItems;

		for (var i = 0; i < items.Length; i++)
		{
			sum += items[i].Value;
		}

		return sum;
	}

	[Benchmark(Description = "Manual property read", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Read")]
	public int ReadManualProperty()
	{
		var sum = 0;
		var items = _manualPropertyItems;

		for (var i = 0; i < items.Length; i++)
		{
			sum += items[i].Value;
		}

		return sum;
	}

	[Benchmark(Baseline = true, Description = "Field write", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Write")]
	public int WriteField()
	{
		var items = _fieldItems;
		var value = NextWriteValue();

		for (var i = 0; i < items.Length; i++)
		{
			items[i].Value = value + i;
		}

		return items[^1].Value;
	}

	[Benchmark(Description = "Auto property write", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Write")]
	public int WriteAutoProperty()
	{
		var items = _autoPropertyItems;
		var value = NextWriteValue();

		for (var i = 0; i < items.Length; i++)
		{
			items[i].Value = value + i;
		}

		return items[^1].Value;
	}

	[Benchmark(Description = "Manual property write", OperationsPerInvoke = ItemsCount)]
	[BenchmarkCategory("Write")]
	public int WriteManualProperty()
	{
		var items = _manualPropertyItems;
		var value = NextWriteValue();

		for (var i = 0; i < items.Length; i++)
		{
			items[i].Value = value + i;
		}

		return items[^1].Value;
	}

	private int NextWriteValue()
	{
		return ++_writeSeed;
	}
}

public sealed class FieldValueModel
{
	public int Value;
}

public sealed class AutoPropertyValueModel
{
	public int Value { get; set; }
}

public sealed class ManualPropertyValueModel
{
	private int _value;

	public int Value
	{
		get => _value;
		set => _value = value;
	}
}
