using System.ComponentModel.DataAnnotations;

namespace TagzApp.Storage.Sqlite;

internal class Tag
{

	[Key]
	public required string Text { get; set; }

}
