namespace hunter;

internal record Pearl {
    public int id { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
    public int e { get; set; }
}

internal record Pos {
    public int x { get; set; }
    public int y { get; set; }
    public int z { get; set; }
}