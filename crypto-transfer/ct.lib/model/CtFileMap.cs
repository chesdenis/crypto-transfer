namespace ct.lib.model;

public record CtFileMap(string FilePath, long FileLength, Dictionary<string, string> Parts);