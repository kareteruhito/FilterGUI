using System.Diagnostics;
using System;
using System.Windows;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using System.Threading.Tasks;

using System.Windows.Media;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Windows.Input.Manipulations;

namespace FilterGUI
{
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name) => PropertyChanged(this, new PropertyChangedEventArgs(name));
        private CompositeDisposable Disposable { get; } = new();
        public ReactiveCommand<EventArgs> LoadedCommand { get; }
        public ReactiveProperty<string> Title { get; private set; }
        public ReactiveProperty<BitmapSource> Image1 { get; private set; }
        public ReactiveProperty<Visibility> Image1Visibility {get; private set;} = new(Visibility.Visible);
        public ReactiveProperty<BitmapSource> Image2 { get; private set; } = new();
        public ReactiveProperty<Visibility> Image2Visibility {get; private set;} = new(Visibility.Hidden);

        // ズーム倍率
        public ReactiveProperty<int> ZoomScale { get; private set;}

        // スライドバーの有効フラグ
        public ReactiveProperty<bool> SliderEnabled {get; private set;} = new(false);

        // ぼかし回数
        public ReactiveProperty<int> BlurNumberOfTimes { get; }
        // ノンローカルミーンフィルタHパラメタ
        public ReactiveProperty<float> NonLocalMeanH { get; }
        // ノンローカルミーンフィルタHパラメタ(スライダー用)
        public ReactiveProperty<int> NonLocalMeanHInt { get; }
        // ラプラシアンフィルタカーネルサイズ
        public ReactiveProperty<int> LaplacianKsize { get; }
        // アンシャープマスキングフィルタKパラメタ
        public ReactiveProperty<double> UnsharpMaskingK { get; }
        // アンシャープマスキングフィルタKパラメタ(スライダー用)
        public ReactiveProperty<int> UnsharpMaskingKInt { get; }
        // ガンマ補正値
        public ReactiveProperty<int> GammaVol { get; }
        // バイラテラルフィルタ実行回数
        public ReactiveProperty<int> BilateralFilterN { get; }
        // バイラテラルフィルタDパラメタ
        public ReactiveProperty<int> BilateralFilterD { get; }
        // バイラテラルフィルタ色パラメタ
        public ReactiveProperty<int> BilateralFilterColor { get; }
        // バイラテラルフィルタ距離パラメタ
        public ReactiveProperty<int> BilateralFilterSpace { get; }
        // メディアンフィルターのカーネルサイズ
        public ReactiveProperty<int> MedianKsize { get; }

        // ドロップコマンド
        public AsyncReactiveCommand<DragEventArgs> DropCommand { get; }

        // フィルターOnOffボタンのテキスト
        public ReactiveProperty<string> FilterOnOffText { get; private set; }
        // フィルターOnOffコマンド
        public ReactiveCommand FilterOnOffCommand { get; }

        // 保存コマンド
        public AsyncReactiveCommand SaveCommand { get; }

        // フィルターフラグ
        public ReactiveProperty<bool> FilterFlag { get; private set; } = new(false);

        // フィルターコマンド
        public AsyncReactiveCommand FilterCommand { get; }

        // コピーコマンド
        public ReactiveCommand CopyCommand { get; }

        // キャンバス高さ
        public ReactiveProperty<double> CanvasHeight { get; private set; }

        // キャンバス幅
        public ReactiveProperty<double> CanvasWidth { get; private set; }
        
        // private SettingInfo _si;
        private GraphicsModel _graphicsModel = new();
        private string _filename;

        public MainWindowViewModel()
        {
            PropertyChanged += (o, e) => {};
            Title = new ReactiveProperty<string>("Title").AddTo(Disposable);

            // ロードコマンドの初期化
            LoadedCommand = new ReactiveCommand<EventArgs>()
                .WithSubscribe(_ => Loaded())
                .AddTo(Disposable);
            // キャンバス高さ初期化
            CanvasHeight = new ReactiveProperty<double>()
                .AddTo(Disposable);
            // キャンバス幅初期化
            CanvasWidth = new ReactiveProperty<double>()
                .AddTo(Disposable);
            // ズーム倍率の初期化
            ZoomScale = new ReactiveProperty<int>(1).AddTo(Disposable);
            ZoomScale.Subscribe(
                _ => {
                    if (Image1 == null) return;
                    if (Image1.Value == null) return;

                    // キャンバスサイズの変更
                    CanvasHeight.Value = (double)Image1.Value.PixelHeight * (double)ZoomScale.Value;
                    CanvasWidth.Value = (double)Image1.Value.PixelWidth * (double)ZoomScale.Value;
                }
            );

            // オリジナルイメージの初期化
            Image1 = new ReactiveProperty<BitmapSource>().AddTo(Disposable);
            Image1.Subscribe(
                async img => {
                    if (img == null) return;
                    
                    // キャンバスサイズの変更
                    CanvasHeight.Value = (double)Image1.Value.PixelHeight * (double)ZoomScale.Value;
                    CanvasWidth.Value = (double)Image1.Value.PixelWidth * (double)ZoomScale.Value;

                    SliderEnabled.Value = false;

                    Image2.Value = await Task.Run(() => _graphicsModel.OpenCVFilter(img));
                    FilterFlag.Value = false;

                    SliderEnabled.Value = true;
                }
            );
            Image1Visibility.AddTo(Disposable);
            // フィルターイメージの初期化
            Image2.AddTo(Disposable);
            Image2Visibility.AddTo(Disposable);

            // ぼかし回数の初期化
            BlurNumberOfTimes = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BlurNumberOfTimes)
                .AddTo(Disposable);
            BlurNumberOfTimes.Subscribe(_ => {FilterFlag.Value = true;});

            //ノンローカルミーンフィルタHパラメタの初期化
            NonLocalMeanH = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.NonLocalMeanH)
                .AddTo(Disposable);
            NonLocalMeanH.Subscribe( x => {
                FilterFlag.Value = true;
                if (NonLocalMeanHInt == null) return;
                var intValue = (int)(x * 10f);
                if (NonLocalMeanHInt.Value != intValue)
                    NonLocalMeanHInt.Value = intValue;
            });
            NonLocalMeanHInt = new ReactiveProperty<int>((int)(NonLocalMeanH.Value * 10f))
                .AddTo(Disposable);
            NonLocalMeanHInt.Subscribe( x => {
                FilterFlag.Value = true;
                if (NonLocalMeanH == null) return;
                var floatValue = (float)x / 10f;
                if (NonLocalMeanH.Value != floatValue)
                    NonLocalMeanH.Value = floatValue;
            });

            // ラプラシアンカーネルサイズの初期化
            LaplacianKsize = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.LaplacianKsize)
                .AddTo(Disposable);
            LaplacianKsize.Subscribe(_ => {FilterFlag.Value = true;});

            // アンシャープマスキングフィルタKパラメタの初期化
            UnsharpMaskingK = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.UnsharpMaskingK)
                .AddTo(Disposable);
            UnsharpMaskingK.Subscribe( x => {
                FilterFlag.Value = true;
                if (UnsharpMaskingKInt == null) return;
                var intValue = (int)(x*10d);
                if (UnsharpMaskingKInt.Value != intValue)
                    UnsharpMaskingKInt.Value = intValue;
            });
            UnsharpMaskingKInt = new ReactiveProperty<int>((int)(UnsharpMaskingK.Value * 10d))
                .AddTo(Disposable);
            UnsharpMaskingKInt.Subscribe( x => {
                FilterFlag.Value = true;
                if (UnsharpMaskingK == null) return;
                var doubleValue = (double)x / 10d;
                if (UnsharpMaskingK.Value != doubleValue)
                    UnsharpMaskingK.Value = doubleValue;
            });

            // ガンマ補正値の初期化
            GammaVol = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.GammaVol)
                .AddTo(Disposable);
            GammaVol.Subscribe(_ => {FilterFlag.Value = true;});

            // バイラテラルフィルタ実行回数の初期化
            BilateralFilterN = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BilateralFilterN)
                .AddTo(Disposable);
            BilateralFilterN.Subscribe(_ => {FilterFlag.Value = true;});

            // バイラテラルフィルタDパラメタの初期化
            BilateralFilterD = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BilateralFilterD)
                .AddTo(Disposable);
            BilateralFilterD.Subscribe(_ => {FilterFlag.Value = true;});

            // バイラテラルフィルタ色パラメタ
            BilateralFilterColor = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BilateralFilterColor)
                .AddTo(Disposable);
            BilateralFilterColor.Subscribe(_ => {FilterFlag.Value = true;});

            // バイラテラルフィルタ距離パラメタ
            BilateralFilterSpace = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BilateralFilterSpace)
                .AddTo(Disposable);
            BilateralFilterSpace.Subscribe(_ => {FilterFlag.Value = true;});

            // メディアンフィルターカーネルサイズの初期化
            MedianKsize = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.MedianKsize)
                .AddTo(Disposable);
            MedianKsize.Subscribe( _ => { FilterFlag.Value = true; } );

            // ドロップコマンドを初期化
            DropCommand = SliderEnabled
                .CombineLatest(Image1, (x, y) => (x|y == null))
                .ToAsyncReactiveCommand<DragEventArgs>()
                .WithSubscribe(
                    async e=>{
                
                        if (e.Data.GetDataPresent(DataFormats.FileDrop) == false) return;
                        string[] args = (string[])e.Data.GetData(DataFormats.FileDrop);

                        List<string> exts = new() {".JPEG", ".JPG", ".PNG", ".BMP"};

                        var files = args.Where(e => exts.Contains(Path.GetExtension(e).ToUpper()));
                        if (files.Any() == false) return;

                        if (files.Count() == 1)
                        {
                            _filename = files.First();
                            Title.Value = _filename;
                            Image1.Value = await Task.Run(() => GraphicsModel.LoadBitmapSource(_filename));
                        }
                        else
                        {
                            var i = 0;
                            foreach(var file in files)
                            {
                                _filename = file;

                                Image1.Value = await Task.Run(() => GraphicsModel.LoadBitmapSource(_filename));
                                // フィルターの実行
                                await FilterCommand.ExecuteAsync();

                                var b = await Task.Run(() => GraphicsModel.SaveBitmapSource(Image2.Value, _filename));
                                if (b)
                                {
                                    Title.Value = String.Format("{0}/{1} {2}",++i,files.Count(), Path.GetFileName(file));
                                }
                            }
                        }
                    }
                )
                .AddTo(Disposable);
            
            // フィルターのOnOffボタンのテキストを初期化
            FilterOnOffText = new ReactiveProperty<string>("フィルターOFF")
                .AddTo(Disposable);

            // フィルターのOnOffコマンドの初期化
            FilterOnOffCommand = Image1
                .CombineLatest(Image2, (x, y) => (x != null & y != null))
                .ToReactiveCommand()
                .WithSubscribe(
                    ()=>{
                        // Image1,Image2の表示・非表示の切り替え
                        if (Image1Visibility.Value == Visibility.Visible)
                        {
                            Image1Visibility.Value = Visibility.Hidden;
                            Image2Visibility.Value = Visibility.Visible;
                            FilterOnOffText.Value = "フィルターON";
                        }
                        else
                        {
                            Image1Visibility.Value = Visibility.Visible;
                            Image2Visibility.Value = Visibility.Hidden;
                            FilterOnOffText.Value = "フィルターOFF";
                        }
                    }
                )
                .AddTo(Disposable);


            // 保存コマンドを初期化
            SaveCommand = Image2
                .Select(x => x != null)
                .ToAsyncReactiveCommand()
                .WithSubscribe(
                    async () => {
                        SliderEnabled.Value = false;
                        var dst = Image2.Value;
                        var b = await Task.Run(() => GraphicsModel.SaveBitmapSource(dst, _filename));
                        if (b)
                        {
                            Title.Value = String.Format("Save Success:{0}", Path.GetFileName(_filename));
                        }
                        SliderEnabled.Value = false;
                    }
                )
                .AddTo(Disposable);
            
            
            // フィルターコマンドを初期化
            FilterCommand = FilterFlag
                .Select(x => x)
                .ToAsyncReactiveCommand()
                .WithSubscribe(
                    async () => {
                        SliderEnabled.Value = false;

                        Image2.Value = await Task.Run(() => _graphicsModel.OpenCVFilter(Image1.Value));
                        FilterFlag.Value = false;

                        SliderEnabled.Value = true;
                        
                        Image1Visibility.Value = Visibility.Hidden;
                        Image2Visibility.Value = Visibility.Visible;
                        FilterOnOffText.Value = "フィルターON";
                    }
                )
                .AddTo(Disposable);
            
            // コピーコマンドを初期化
            CopyCommand = Image2
                .Select(x => x != null)
                .ToReactiveCommand()
                .WithSubscribe(
                    () => {
                        BitmapSource source = GraphicsModel.ConvertRGBA(Image2.Value);
                        PngBitmapEncoder encoder = new();
                        encoder.Frames.Add(BitmapFrame.Create(source));
                        using var stream = new MemoryStream();
                        encoder.Save(stream);
                        Clipboard.SetData("PNG", stream);
                    }
                )
                .AddTo(Disposable);
        }
        public void Loaded()
        {
            Debug.WriteLine("Loaded()");

            if (_graphicsModel.Load())
            {
                var type = _graphicsModel.GetType();
                var thisType = this.GetType();

                foreach(var e in type.GetProperties())
                {
                    var property = type.GetProperty(e.Name);
                    var v = property.GetValue(_graphicsModel);

                    var thisProperty = thisType.GetProperty(e.Name);
                    var thisValue = thisProperty.GetValue(this);
                    if (thisValue is ReactiveProperty<int> tv)
                    {
                        tv.Value = (int)v;
                    }
                }
            }
        }
        public void Dispose()
        {
            Debug.WriteLine("Dispose()");
            _graphicsModel.Save();
            Disposable.Dispose();
        }
    }
}
