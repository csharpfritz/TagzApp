using System.ComponentModel.DataAnnotations;

namespace TagzApp.Storage.Postgres;

public class PgQueueItem
{

	[MaxLength(20)]
	public string Provider { get; set; }

	[MaxLength(200)]
	public string ProviderId { get; set; }

	[MaxLength(500)]
	public string SpeakerNotes { get; set; }

	public bool IsCompleted { get; set; } = false;

	public int OrderBy { get; set; } = 1;

}
