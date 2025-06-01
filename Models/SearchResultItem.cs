using skinhunter.Services;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Media;


namespace skinhunter.Models
{
    public enum SearchResultType
    {
        Champion,
        Skin
    }

    public partial class SearchResultItem : ObservableObject
    {
        public int Id { get; }
        public string Name { get; }
        public SearchResultType Type { get; }
        public string DisplayType { get; }

        [ObservableProperty]
        private BitmapImage? _imageSource;

        private readonly string? _imagePath;

        public int ChampionId { get; }
        public Skin? OriginalSkinObject { get; }
        public ChampionSummary? OriginalChampionObject { get; }


        public SearchResultItem(ChampionSummary champion)
        {
            Id = champion.Id;
            Name = champion.Name;
            Type = SearchResultType.Champion;
            DisplayType = "Champion";
            _imagePath = champion.SquarePortraitPath;
            ChampionId = champion.Id;
            OriginalChampionObject = champion;
        }

        public SearchResultItem(Skin skin, ChampionSummary? parentChampion)
        {
            Id = skin.Id;
            Name = skin.Name;
            Type = SearchResultType.Skin;
            DisplayType = "Champion Skin";
            _imagePath = skin.TilePath;
            ChampionId = skin.ChampionId;
            OriginalSkinObject = skin;
            OriginalChampionObject = parentChampion;
        }

        private bool _isImageLoadingOrLoaded = false;

        public async Task LoadImageAsync()
        {
            if (_isImageLoadingOrLoaded || string.IsNullOrEmpty(_imagePath))
            {
                return;
            }

            _isImageLoadingOrLoaded = true;

            BitmapImage? loadedBitmap = null;
            Uri? imageUri = null;

            try
            {
                string fullUrl = CdragonDataService.GetAssetUrl(_imagePath);
                if (Uri.TryCreate(fullUrl, UriKind.Absolute, out imageUri))
                {

                    if (System.Windows.Application.Current != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            BitmapImage bitmap = new();
                            bitmap.BeginInit();
                            bitmap.UriSource = imageUri;
                            bitmap.DecodePixelWidth = 64;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            try
                            {
                                bitmap.EndInit();
                                if (bitmap.CanFreeze)
                                {
                                    bitmap.Freeze();
                                }
                                loadedBitmap = bitmap;
                            }
                            catch (Exception exEndInit)
                            {
                                Debug.WriteLine($"Error en EndInit para imagen {imageUri}: {exEndInit.Message}");
                                loadedBitmap = null;
                            }
                        });
                    }
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creando Uri o despachando carga de imagen {_imagePath}: {ex.Message}");
                loadedBitmap = null;
            }

            ImageSource = loadedBitmap;
        }
    }
}