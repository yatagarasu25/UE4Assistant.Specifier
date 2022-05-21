using FluentAssertions;

namespace UE4Assistant.Tests
{
	[TestFixture]
	public class ParseSpecifierStringTest
	{
		[Datapoints]
		public string[] data = new string[] {
			"\tUPROPERTY()",
			"\tUPROPERTY(VisibleInstanceOnly, BlueprintReadOnly)"
		};

		[Theory]
		public void ParseSimpleSpecifierLine(string str)
		{
			var specifiers = Specifier.FindAll(str).ToList();

			specifiers.Should().HaveCount(1);
			specifiers[0].si.Should().Be(1);
			specifiers[0].ei.Should().Be(str.Length);
		}
	}
}
