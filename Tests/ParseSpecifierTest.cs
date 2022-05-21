using FluentAssertions;
using SystemEx;

namespace UE4Assistant.Tests
{
	[TestFixture]
	public class ParseSimpleSpecifierTest
	{
		[Datapoints]
		public string[] data = new[] {
			"UPROPERTY()", "UPROPERTY  ()", "UPROPERTY( )", "UPROPERTY (  )"
		};

		[Theory]
		public void AssertValidSimpleSpecifier(string str)
		{
			Specifier.TryParse(str.tokenize(), out var specifier).Should().BeTrue();
			specifier.type.Should().Be("UPROPERTY");
			specifier.data.Should().NotBeNull().And.HaveCount(0);
		}
	}


	[TestFixture]
	public class ParseOneParamSpecifierTest
	{
		[Datapoints]
		public string[] data = new[] {
			"UFUNCTION(BlueprintReadWrite)",
			"UFUNCTION  (BlueprintReadWrite)",
			"UFUNCTION(  BlueprintReadWrite)",
			"UFUNCTION ( BlueprintReadWrite )"
		};

		[Theory]
		public void AssertParseOneParamSpecifier(string str)
		{
			Specifier.TryParse(str.tokenize(), out var specifier).Should().BeTrue();
			specifier.type.Should().Be("UFUNCTION");
			specifier.data.Should().NotBeNull().And.HaveCount(1).And.ContainKey("BlueprintReadWrite");
		}
	}

	[TestFixture]
	public class ParseOneKeyValueParamSpecifierTest
	{
		[Datapoints]
		public string[] data = new[] {
			"UFUNCTION(Category=\"Test\")",
			"UFUNCTION(Category  =\"Test\")",
			"UFUNCTION( Category=  \"Test\" )",
			"UFUNCTION( Category =  \"Test\"  )"
		};

		[Theory]
		public void AssertParseOneKeyValueParamSpecifier(string str)
		{
			Specifier.TryParse(str.tokenize(), out var specifier).Should().BeTrue();
			specifier.type.Should().Be("UFUNCTION");

			specifier.data.Should().NotBeNull().And.HaveCount(1)
				.And.ContainKey("Category");

			Assert.AreEqual("Test", specifier.data["Category"]);
		}
	}

	[TestFixture]
	public class ParseOneOneKeyObjectParamSpecifierTest
	{
		[Datapoints]
		public string[] data = new[] {
			"UFUNCTION(meta=(ExposeOnSpawn))",
			"UFUNCTION( meta = ( ExposeOnSpawn ) )",
			"UFUNCTION( meta=( ExposeOnSpawn) )",
			"UFUNCTION(meta= ( ExposeOnSpawn ))",
		};

		[Theory]
		public void AssertParseOneKeyObjectParamSpecifier(string str)
		{
			Specifier.TryParse(str.tokenize(), out var specifier).Should().BeTrue();
			specifier.type.Should().Be("UFUNCTION");

			specifier.data.Should().NotBeNull().And.HaveCount(1)
				.And.ContainKey("meta");

			var values = specifier.data["meta"] as Dictionary<string, object>;
			Assert.AreNotEqual(null, values);
			Assert.AreEqual(1, values.Count);
			Assert.AreEqual(true, values.ContainsKey("ExposeOnSpawn"));
		}
	}
}
