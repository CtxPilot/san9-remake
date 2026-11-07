namespace San9.Content.Packages;

public enum ContentImportStage { Checking, Textures, Audio, Scenarios, Movies, Verifying, Complete, Failed }

/// <summary>Task state for hosts; log wording is not a machine protocol.</summary>
public sealed record ContentImportProgress(ContentImportStage Stage, string Message);
