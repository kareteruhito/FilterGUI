using System;
using System.Diagnostics;
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
            
            /*
            double[,] kernel = {
                    {0.0/5.0, 1.0/5.0, 0.0/5.0},
                    {1.0/5.0, 1.0/5.0, 1.0/5.0},
                    {0.0/5.0, 1.0/5.0, 0.0/5.0},
            };
            */

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
            //Cv2.Laplacian(src, dst, MatType.CV_8UC1, LaplacianKsize);
            Cv2.Laplacian(src, dst, MatType.CV_64F, LaplacianKsize);
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

        // ガンマ補正
        static private void GammaCorrection(ref Mat src, int GammaVal=10)
        {
            var x = new int[256];
            for(var i = 0; i < x.Length; i++) x[i] = i;

            double gamma = GammaVal < 0 ? ( 1.0 / (((double)GammaVal/10.0) * -1.0) ) : ((double)GammaVal/10.0);

            var y = new int[256];
            for(var j = 0; j < y.Length; j++)
            {
                y[j] = (int)(Math.Pow( (double)x[j] / 255.0, gamma ) * 255.0);
                //Debug.Print("y[{0}]:{1} gamma:{2} Pow:{3} {4}", j, y[j], gamma, Math.Pow( x[j] / 255, gamma ), (double)x[j] / 255.0);
            }

            var dst = new Mat();
            Cv2.LUT(src, InputArray.Create(y), dst);

            dst.ConvertTo(src, MatType.CV_8UC1);
            
        }

        static public BitmapSource OpenCVFilter(BitmapSource src, SettingInfo si)
        {
            var mat = BitmapSourceConverter.ToMat(src);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2GRAY);

            // オープニング
            //var mKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            //Cv2.MorphologyEx(mat, mat, MorphTypes.Open, mKernel, null, 1);

            // クロージング
            //Cv2.MorphologyEx(mat, mat, MorphTypes.Close, mKernel, null, 1);

            // 縮小・拡大
            var h = mat.Height;
            var w = mat.Width;
            var scale = 1000.0/((double)h);
            var smallH = (int)((double)h * scale);
            var smallW = (int)((double)w * scale);
            Cv2.Resize(mat, mat, new OpenCvSharp.Size(smallW, smallH), 0.0, 0.0, InterpolationFlags.Lanczos4);
            Cv2.Resize(mat, mat, new OpenCvSharp.Size(w, h), 0.0, 0.0, InterpolationFlags.Lanczos4);


            // ぼかし処理
            if (si.BlurNumberOfTimes > 0) OrignalBlur(ref mat, si.BlurNumberOfTimes);

            // ノンローカルミーンフィルタ
            if (si.NonLocalMeanH > 0) NonLocalMeans(ref mat, si.NonLocalMeanH);

            // ラプラシアンフィルタ
            if (si.LaplacianKsize % 2 == 1)
            {
                var edge = Laplacian(ref mat, si.LaplacianKsize);
                if (edge != null)
                {
                    edge.ConvertTo(edge, MatType.CV_8UC1);
                    // 減算
                    //mat = mat - edge;
                    Cv2.Subtract(mat, edge, mat);
                    //Cv2.BitwiseNot(edge, mat);
                    //mat = edge.Clone();
                     if (edge != null) edge.Dispose();
                }
            }

            // アンシャープマスキングフィルタ
            if (si.UnsharpMaskingK > 0) UnSharpMasking(ref mat, si.UnsharpMaskingK);

            // ガンマ補正
            if (si.GammaVol > 10 || si.GammaVol < -10) GammaCorrection(ref mat, si.GammaVol);

            var dst = BitmapSourceConverter.ToBitmapSource(mat);
            dst.Freeze();

            if (mat != null) mat.Dispose();

            return dst;
        }

        static public BitmapSource ConvertRGBA(BitmapSource src)
        {
            using var mat = BitmapSourceConverter.ToMat(src);

            Cv2.CvtColor(mat, mat, ColorConversionCodes.GRAY2RGBA);

            Debug.Print("チャンネル数:{0}", mat.Channels());

            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    //Vec3b pic = mat.At<Vec3b>(y, x);
                    Vec4b pic = mat.At<Vec4b>(y, x);

                    //pic[0] = 0;     // B
                    //pic[1] = 0;     // G
                    //pic[2] = 0;     // R
                    pic[3] = (byte)(Convert.ToByte("255") - pic[0]);   // A

                    mat.Set(y, x, pic);
                }
            }

            return BitmapSourceConverter.ToBitmapSource(mat);
        }
    }
}