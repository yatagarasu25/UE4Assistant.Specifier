using FluentAssertions;

namespace UE4Assistant.Tests
{
	[TestFixture]
	public class ParseSpecifierStringTest
	{
		[Test]
		public void ParseSimpleSpecifierLine()
		{
			const string str = "\tUPROPERTY()";
			var specifiers = Specifier.FindAll(str).ToList();

			specifiers.Should().HaveCount(1);
			specifiers[0].si.Should().Be(1);
			specifiers[0].si.Should().Be(str.Length);
		}

		[Test]
		public void ParseSimpleSpecifierLine_TwoParam()
		{
			const string str = "\tUPROPERTY(VisibleInstanceOnly, BlueprintReadOnly)";
			var specifiers = Specifier.FindAll(str).ToList();

			specifiers.Should().HaveCount(1);
			specifiers[0].si.Should().Be(1);
			specifiers[0].si.Should().Be(str.Length);
		}
	}
}
