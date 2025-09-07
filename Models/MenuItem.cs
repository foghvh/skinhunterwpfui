namespace skinhunter.Models
{
    public class MenuItem
    {
        public string Content { get; set; } = string.Empty;
        public SymbolRegular Icon { get; set; }
        public Type? TargetPageType { get; set; }
    }
}