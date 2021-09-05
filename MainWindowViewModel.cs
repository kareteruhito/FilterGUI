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
        public ReactiveProperty<int> ZoomScale { get; private set;} = new(1);

        // スライドバーの有効フラグ
        public ReactiveProperty<bool> SliderEnabled {get; private set;} = new(false);

        // キャンセルフラグ
        public ReactiveProperty<bool> CancelFlag { get; private set; } = new (false);

        public ReactiveProperty<int> BlurNumberOfTimes { get; private set;}
        public ReactiveProperty<int> NonLocalMeanH { get; private set;}
        public ReactiveProperty<int> LaplacianKsize { get; private set;}
        public ReactiveProperty<int> UnsharpMaskingK { get; private set;}

        // ガンマ補正値
        public ReactiveProperty<int> GammaVol { get; private set;}

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

        private SettingInfo _si;
        private string _filename;

        public MainWindowViewModel()
        {
            PropertyChanged += (o, e) => {};
            Title = new ReactiveProperty<string>("Title").AddTo(Disposable);

            LoadedCommand = new ReactiveCommand<EventArgs>()
                .WithSubscribe(_ => Loaded())
                .AddTo(Disposable);

            _si = new SettingInfo();

            ZoomScale.AddTo(Disposable);

            Image1 = new ReactiveProperty<BitmapSource>().AddTo(Disposable);
            Image1.Subscribe(
                async img => {
                    if (img == null) return;
                    SliderEnabled.Value = false;

                    Image2.Value = await Task.Run(() => GraphicsModel.OpenCVFilter(img, _si));
                    FilterFlag.Value = false;

                    SliderEnabled.Value = true;
                }
            );
            Image1Visibility.AddTo(Disposable);

            Image2.AddTo(Disposable);
            Image2Visibility.AddTo(Disposable);

            // 実行ボタンのテキストを初期化

            ToggleButtonText = new ReactiveProperty<string>("フィルターOFF")
                .AddTo(Disposable);

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

                        Image2.Value = await Task.Run(() => GraphicsModel.OpenCVFilter(Image1.Value, _si));
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
                        using var ms = new System.IO.MemoryStream();
                        pngEnc.Save(ms);
                        Clipboard.SetData("PNG", ms);
                    }
                )
                .AddTo(Disposable);
            
        }
        public void Loaded()
        {
            Debug.WriteLine("Loaded()");

            if (_si.Load())
            {
                /*
                BlurNumberOfTimes.Value = _si.BlurNumberOfTimes;
                NonLocalMeanH.Value = _si.NonLocalMeanH;
                LaplacianKsize.Value = _si.LaplacianKsize;
                UnsharpMaskingK.Value = _si.UnsharpMaskingK;
                */
                var type = _si.GetType();
                var thisType = this.GetType();

                foreach(var e in type.GetProperties())
                {
                    var property = type.GetProperty(e.Name);
                    var v = property.GetValue(_si);
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
            _si.Save();
            Disposable.Dispose();
        }
    }
}
