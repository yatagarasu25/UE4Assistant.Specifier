using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UE4Assistant.Specifier
{
	public record struct A();

	public struct Specifier
	{
		public int startIndex;
		public int endIndex;
		public string type;
		public TagModel tag;
		public Dictionary<string, object> data;

		public override string ToString()
		{
			return type + data.GenerateSpecifier(SpecifierSchema.ReadSpecifierSettings(type));
		}
	}

}
