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

        // 実行ボタンのテキスト
        public ReactiveProperty<string> ToggleButtonText { get; private set; }

        // ズーム倍率
        public ReactiveProperty<int> ZoomScale { get; private set;}

        // スライドバーの有効フラグ
        public ReactiveProperty<bool> SliderEnabled {get; private set;} = new(false);

        // キャンセルフラグ
        public ReactiveProperty<bool> CancelFlag { get; private set; } = new (false);

        // ぼかし回数
        public ReactiveProperty<int> BlurNumberOfTimes { get; }
        // ノンローカルミーンフィルタHパラメタ
        public ReactiveProperty<int> NonLocalMeanH { get; }
        // ラプラシアンフィルタカーネルサイズ
        public ReactiveProperty<int> LaplacianKsize { get; }
        // アンシャープマスキングフィルタKパラメタ
        public ReactiveProperty<int> UnsharpMaskingK { get; }
        // ガンマ補正値
        public ReactiveProperty<int> GammaVol { get; }

        // ドロップコマンド
        public AsyncReactiveCommand<DragEventArgs> DropCommand { get; }

        // フィルター有効コマンド
        public ReactiveCommand FilterViewCommand { get; }

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

            // 実行ボタンのテキストを初期化
            ToggleButtonText = new ReactiveProperty<string>("フィルターOFF")
                .AddTo(Disposable);
            
            // ぼかし回数の初期化
            BlurNumberOfTimes = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.BlurNumberOfTimes)
                .AddTo(Disposable);
            BlurNumberOfTimes.Subscribe(_ => {FilterFlag.Value = true;});

            //ノンローカルミーンフィルタHパラメタの初期化
            NonLocalMeanH = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.NonLocalMeanH)
                .AddTo(Disposable);
            NonLocalMeanH.Subscribe(_ => {FilterFlag.Value = true;});

            // ラプラシアンカーネルサイズの初期化
            LaplacianKsize = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.LaplacianKsize)
                .AddTo(Disposable);
            NonLocalMeanH.Subscribe(_ => {FilterFlag.Value = true;});

            // アンシャープマスキングフィルタKパラメタの初期化
            UnsharpMaskingK = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.UnsharpMaskingK)
                .AddTo(Disposable);
            UnsharpMaskingK.Subscribe(_ => {FilterFlag.Value = true;});

            // ガンマ補正値の初期化
            GammaVol = _graphicsModel.ToReactivePropertyAsSynchronized(m => m.GammaVol)
                .AddTo(Disposable);
            GammaVol.Subscribe(_ => {FilterFlag.Value = true;});

            /*
            BlurNumberOfTimes = new ReactiveProperty<int>(_si.BlurNumberOfTimes).AddTo(Disposable);
            BlurNumberOfTimes.Subscribe(x=>{
                _si.BlurNumberOfTimes = x;
                FilterFlag.Value = true;
            });
            NonLocalMeanH = new ReactiveProperty<int>(_si.NonLocalMeanH).AddTo(Disposable);
            NonLocalMeanH.Subscribe(x=>{
                _si.NonLocalMeanH = x;
                FilterFlag.Value = true;
            });

            LaplacianKsize = new ReactiveProperty<int>(_si.LaplacianKsize).AddTo(Disposable);
            LaplacianKsize.Subscribe(x=>{
                _si.LaplacianKsize = x;
                FilterFlag.Value = true;
            });

            UnsharpMaskingK = new ReactiveProperty<int>(_si.UnsharpMaskingK).AddTo(Disposable);
            UnsharpMaskingK.Subscribe(x=>{
                _si.UnsharpMaskingK = x;
                FilterFlag.Value = true;
            });

            // ガンマ補正値を初期化
            GammaVol = new ReactiveProperty<int>(_si.GammaVol).AddTo(Disposable);
            GammaVol.Subscribe(x=>{
                _si.GammaVol = x;
                FilterFlag.Value = true;
            });
            */

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
            
            // フィルタービューコマンドの初期化
            FilterViewCommand = Image1
                .CombineLatest(Image2, (x, y) => (x != null & y != null))
                .ToReactiveCommand()
                .WithSubscribe(
                    ()=>{
                        // Image1,Image2の表示・非表示の切り替え
                        if (Image1Visibility.Value == Visibility.Visible)
                        {
                            Image1Visibility.Value = Visibility.Hidden;
                            Image2Visibility.Value = Visibility.Visible;
                            ToggleButtonText.Value = "フィルターON";
                        }
                        else
                        {
                            Image1Visibility.Value = Visibility.Visible;
                            Image2Visibility.Value = Visibility.Hidden;
                            ToggleButtonText.Value = "フィルターOFF";
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
                        ToggleButtonText.Value = "フィルターON";
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
                        PngBitmapEncoder pngEnc = new();
                        pngEnc.Frames.Add(BitmapFrame.Create(source));
                        using var ms = new MemoryStream();
                        pngEnc.Save(ms);
                        Clipboard.SetData("PNG", ms);
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
                    //property.SetValue(this, v);

                    var thisProperty = thisType.GetProperty(e.Name);
                    var thisV = thisProperty.GetValue(this);
                    if (thisV is ReactiveProperty<int>)
                    {
                        var xv = (ReactiveProperty<int>)thisV;
                        xv.Value = (int)v;
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
