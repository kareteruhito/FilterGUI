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


namespace FilterGUI
{
    public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name) => PropertyChanged(this, new PropertyChangedEventArgs(name));
        private CompositeDisposable Disposable { get; } = new();
        public ReactiveCommand<EventArgs> LoadedCommand { get; }
        public ReactiveProperty<string> Title { get; private set; }
        public ReactiveCommand<DragEventArgs> DropCommand {get;} = new();
        public ReactiveProperty<BitmapSource> Image1 { get; private set; } = new();
        public ReactiveProperty<Visibility> Image1Visibility {get; private set;} = new(Visibility.Visible);
        public ReactiveProperty<BitmapSource> Image2 { get; private set; } = new();
        public ReactiveProperty<Visibility> Image2Visibility {get; private set;} = new(Visibility.Hidden);
        public ReactiveCommand ButtonCommand {get;}
        public ReactiveProperty<string> ToggleButtonText { get; private set; } = new("フィルターOFF");
        public ReactiveProperty<int> ZoomScale { get; private set;} = new(1);
        public ReactiveProperty<bool> SliderEnabled {get; private set;} = new(false);
        public ReactiveProperty<int> BlurNumberOfTimes { get; private set;}
        public ReactiveProperty<int> NonLocalMeanH { get; private set;}
        public ReactiveProperty<int> LaplacianKsize { get; private set;}
        public ReactiveProperty<int> UnsharpMaskingK { get; private set;}
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
            /*
            {
                BlurNumberOfTimes.Value = 12,
                NonLocalMeanH.Value = 16,
                LaplacianKsize.Value = 0,
                UnsharpMaskingK.Value = 45,
            };
            */

            ZoomScale.AddTo(Disposable);

            DropCommand.Subscribe<DragEventArgs>(async e=>{
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
                    SliderEnabled.Value = false;
                    Image1Visibility.Value = Visibility.Hidden;
                    Image2Visibility.Value = Visibility.Hidden;
                    var i = 0;
                    foreach(var file in files)
                    {
                        _filename = file;

                        var src = await Task.Run(() => GraphicsModel.LoadBitmapSource(_filename));
                        var dst = await Task.Run(() => GraphicsModel.OpenCVFilter(src, _si));

                        var b = await Task.Run(() => GraphicsModel.SaveBitmapSource(dst, _filename));
                        if (b)
                        {
                            Title.Value = String.Format("{0}/{1} {2}",++i,files.Count(), Path.GetFileName(file));
                        }
                    }
                    Image1Visibility.Value = Visibility.Hidden;
                    Image2Visibility.Value = Visibility.Visible;
                    ToggleButtonText.Value = "フィルターON";
                    SliderEnabled.Value = true;
                }

            }).AddTo(Disposable);

            Image1.AddTo(Disposable);
            Image1.Subscribe(async img=>{
                if (img == null) return;
                Image2.Value = await Task.Run(()=>GraphicsModel.OpenCVFilter(img, _si));
            });
            Image1Visibility.AddTo(Disposable);

            Image2.AddTo(Disposable);
            Image2Visibility.AddTo(Disposable);

            ToggleButtonText.AddTo(Disposable);

            ButtonCommand = Image1.Select(x => x != null).ToReactiveCommand().AddTo(Disposable);
            ButtonCommand.Subscribe(()=>{
                if (Image1Visibility.Value == Visibility.Visible)
                {
                    Image1Visibility.Value = Visibility.Hidden;
                    Image2Visibility.Value = Visibility.Visible;
                    ToggleButtonText.Value = "フィルターON";
                    SliderEnabled.Value = true;
                }
                else
                {
                    Image1Visibility.Value = Visibility.Visible;
                    Image2Visibility.Value = Visibility.Hidden;
                    ToggleButtonText.Value = "フィルターOFF";
                    SliderEnabled.Value = false;
                }
            });

            BlurNumberOfTimes = new ReactiveProperty<int>(_si.BlurNumberOfTimes).AddTo(Disposable);
            BlurNumberOfTimes.Subscribe(async x=>{
                _si.BlurNumberOfTimes = x;

                if (Image1.Value == null) return;
                Image2.Value = await Task.Run(()=>GraphicsModel.OpenCVFilter(Image1.Value, _si));
            });
            NonLocalMeanH = new ReactiveProperty<int>(_si.NonLocalMeanH).AddTo(Disposable);
            NonLocalMeanH.Subscribe(async x=>{
                _si.NonLocalMeanH = x;

                if (Image1.Value == null) return;
                Image2.Value = await Task.Run(()=>GraphicsModel.OpenCVFilter(Image1.Value, _si));
            });

            LaplacianKsize = new ReactiveProperty<int>(_si.LaplacianKsize).AddTo(Disposable);
            LaplacianKsize.Subscribe(async x=>{
                _si.LaplacianKsize = x;

                if (Image1.Value == null) return;
                Image2.Value = await Task.Run(()=>GraphicsModel.OpenCVFilter(Image1.Value, _si));
            });

            UnsharpMaskingK = new ReactiveProperty<int>(_si.UnsharpMaskingK).AddTo(Disposable);
            UnsharpMaskingK.Subscribe(async x=>{
                _si.UnsharpMaskingK = x;

                if (Image1.Value == null) return;
                Image2.Value = await Task.Run(()=>GraphicsModel.OpenCVFilter(Image1.Value, _si));
            });

        }
        public void Loaded()
        {
            Debug.WriteLine("Loaded()");

            if (_si.Load())
            {
                BlurNumberOfTimes.Value = _si.BlurNumberOfTimes;
                NonLocalMeanH.Value = _si.NonLocalMeanH;
                LaplacianKsize.Value = _si.LaplacianKsize;
                UnsharpMaskingK.Value = _si.UnsharpMaskingK;
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
