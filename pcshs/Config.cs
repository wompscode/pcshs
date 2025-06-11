namespace pcshs;

public struct Config
{
    public string DataDirectory { get; set; }
    public string ServerValue { get; set; }
    public string NotFoundPage { get; init; }
    public int Port { get; set; }
}