using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;

namespace FilterGUI
{
    class GraphicsModel
    {
        static public BitmapSource LoadBitmapSource(string file)
        {
            Mat mat = new(file);

            var fb = BitmapSourceConverter.ToBitmapSource(mat);
            fb.Freeze();
            return fb;
        }
        static public bool SaveBitmapSource(BitmapSource bi, string filename)
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            dir = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd"));
            if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);

            var f = Path.GetFileNameWithoutExtension(filename);
            var path = Path.Combine(dir, (f + ".png"));
            

            using (FileStream stream = new FileStream(path, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bi));
                encoder.Save(stream);
            }

            return File.Exists(path);
        }

        static private void OrignalBlur(ref Mat mat, int BlurNumberOfTimes=12)
        {
            double[,] kernel = {
                    {0.0/16.0, 1.0/16.0,0.0/16.0},
                    {1.0/16.0,12.0/16.0,1.0/16.0},
                    {0.0/16.0, 1.0/16.0,0.0/16.0},
            };

            for(int x=0; x<BlurNumberOfTimes; x++)
            {
                Cv2.Filter2D(mat, mat, -1, InputArray.Create(kernel));
            }
        }
        static private void NonLocalMeans(ref Mat mat, int NonLocalMeanH=16)
        {
            if (NonLocalMeanH == 0) return;
            Cv2.FastNlMeansDenoising(mat, mat, (float)NonLocalMeanH);
        }
        static private Mat Laplacian(ref Mat src, int LaplacianKsize)
        {
            if (LaplacianKsize == 0) return null;
            if (LaplacianKsize % 2 != 1) return null;

            Mat dst = src.Clone();
            Cv2.Laplacian(src, dst, MatType.CV_8UC1, LaplacianKsize);
            return dst;
        }
        static private void UnSharpMasking(ref Mat mat, int UnsharpMaskingK=45)
        {
            if (UnsharpMaskingK == 0) return;

            double k = (double)UnsharpMaskingK / 10.0;
            double[,] unsharpKernel = { { -k/9.0,        -k/9.0, -k/9.0},
                                        { -k/9.0, 1.0+8.0*k/9.0, -k/9.0},
                                        { -k/9.0,        -k/9.0, -k/9.0},
            };
            Cv2.Filter2D(mat, mat, -1, InputArray.Create(unsharpKernel));
        }

        static public BitmapSource OpenCVFilter(BitmapSource src, SettingInfo si)
        {
            var mat = BitmapSourceConverter.ToMat(src);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2GRAY);

            // ぼかし処理
            OrignalBlur(ref mat, si.BlurNumberOfTimes);

            // アンシャープマスキングフィルタ
            UnSharpMasking(ref mat, si.UnsharpMaskingK);
            // ノンローカルミーンフィルタ
            NonLocalMeans(ref mat, si.NonLocalMeanH);
            // ラプラシアンフィルタ
            var edge = Laplacian(ref mat, si.LaplacianKsize);
            if (edge != null)
            {
                // 減算
                //mat = mat - edge;
                Cv2.Subtract(mat, edge, mat);
                //Cv2.BitwiseNot(edge, mat);
            }

            var dst = BitmapSourceConverter.ToBitmapSource(mat);
            dst.Freeze();

            if (mat != null) mat.Dispose();
            if (edge != null) edge.Dispose();

            return dst;
        }
    }
}