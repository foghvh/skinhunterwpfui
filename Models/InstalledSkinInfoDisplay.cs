using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;

namespace skinhunter.Models
{
    public partial class InstalledSkinInfoDisplay : ObservableObject
    {
        public InstalledSkinInfo SkinInfo { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? SelectionChanged;

        public string DisplayName => string.IsNullOrEmpty(SkinInfo.ChromaName) || SkinInfo.ChromaName.Equals("Default", StringComparison.OrdinalIgnoreCase)
                                    ? SkinInfo.SkinName
                                    : $"{SkinInfo.SkinName} ({SkinInfo.ChromaName})";
        public string FileName => SkinInfo.FileName;
        public string FolderName => SkinInfo.FolderName;
        public string ImageUrl => SkinInfo.ImageUrl;
        public string ChampionName { get; set; } = "Unknown Champion";

        public InstalledSkinInfoDisplay(InstalledSkinInfo skinInfo)
        {
            SkinInfo = skinInfo;
        }
    }
}