﻿namespace TagzApp.ViewModels.Data;

public class QueueItem
{

	public required ContentModel Content { get; set; }

	public string SpeakerNotes { get; set; } = string.Empty;

	public int OrderBy { get; set; } = 1;

	public bool IsCompleted { get; set; } = false;

}