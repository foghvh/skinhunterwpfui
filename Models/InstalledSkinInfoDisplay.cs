/// skinhunter Start of Models\InstalledSkinInfoDisplay.cs ///
namespace skinhunter.Models
{
    public partial class InstalledSkinInfoDisplay : ObservableObject // Asegúrate que ObservableObject está disponible o quítalo si no hay INPC
    {
        public InstalledSkinInfo Skin { get; }

        [ObservableProperty]
        private bool _isSelected;

        public string DisplayName => string.IsNullOrEmpty(Skin.ChromaName) || Skin.ChromaName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                                    ? Skin.SkinName
                                    : $"{Skin.SkinName} ({Skin.ChromaName})";
        public string FileName => Skin.FileName;
        public string ImageUrl => Skin.ImageUrl;
        public string ChampionName { get; set; } = "Unknown Champion";

        public InstalledSkinInfoDisplay(InstalledSkinInfo skin)
        {
            Skin = skin;
        }
    }
}
/// skinhunter End of Models\InstalledSkinInfoDisplay.cs ///