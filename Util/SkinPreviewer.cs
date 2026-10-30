using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Elysium.Core.Utils;

namespace PLDUOBAO.Util
{
    public static class SkinPreviewer
    {
        private const int Scale = 4;

        public static RenderTargetBitmap? RenderSkinPreview(string skinPath, bool isSlim, bool is3D, double yawDeg = -30, double animPhase = 0)
        {
            if (!File.Exists(skinPath)) return null;

            try
            {
                using var stream = File.OpenRead(skinPath);
                var skinSource = new Bitmap(stream);
                var result = new RenderTargetBitmap(new PixelSize(160, 240), new Vector(96, 96));

                using (var context = result.CreateDrawingContext())
                {
                    if (!is3D)
                    {
                        Render2D(context, skinSource, isSlim);
                    }
                    else
                    {
                        Render3D(context, skinSource, isSlim, yawDeg, animPhase);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                LogSys.Log($"渲染皮肤预览失败: {ex.Message}");
                return null;
            }
        }

        private static void Render2D(DrawingContext context, Bitmap skin, bool isSlim)
        {
            double s = 6;
            using (context.PushTransform(Matrix.CreateTranslation(80 - (8 * s), 120 - (16 * s))))
            {
                DrawPart(context, skin, new Rect(8, 8, 8, 8), new Rect(4 * s, 0, 8 * s, 8 * s));
                DrawPart(context, skin, new Rect(40, 8, 8, 8), new Rect(4 * s, 0, 8 * s, 8 * s));
                DrawPart(context, skin, new Rect(20, 20, 8, 12), new Rect(4 * s, 8 * s, 8 * s, 12 * s));
                if (skin.Size.Height >= 64) DrawPart(context, skin, new Rect(20, 36, 8, 12), new Rect(4 * s, 8 * s, 8 * s, 12 * s));
                int w = isSlim ? 3 : 4;
                if (skin.Size.Height >= 64) DrawPart(context, skin, new Rect(36, 52, w, 12), new Rect((4 - w) * s, 8 * s, w * s, 12 * s));
                else DrawPart(context, skin, new Rect(44, 20, w, 12), new Rect((4 - w) * s, 8 * s, w * s, 12 * s));
                DrawPart(context, skin, new Rect(44, 20, w, 12), new Rect(12 * s, 8 * s, w * s, 12 * s));
                if (skin.Size.Height >= 64) DrawPart(context, skin, new Rect(20, 52, 4, 12), new Rect(4 * s, 20 * s, 4 * s, 12 * s));
                else DrawPart(context, skin, new Rect(4, 20, 4, 12), new Rect(4 * s, 20 * s, 4 * s, 12 * s));
                DrawPart(context, skin, new Rect(4, 20, 4, 12), new Rect(8 * s, 20 * s, 4 * s, 12 * s));
            }
        }

        private static void Render3D(DrawingContext context, Bitmap skin, bool isSlim, double yawDeg, double phase)
        {
            using (context.PushTransform(Matrix.CreateTranslation(80, 80)))
            {
                double yaw = yawDeg * Math.PI / 180.0;
                double walkAngle = Math.Sin(phase) * 0.7;
        
                // 回到稳定的固定顺序绘制，不再使用动态排序
                DrawPart3D(context, skin, BodyPart.LeftLeg, new Vector3D(-2, 12, 0), isSlim, yaw, -walkAngle);
                DrawPart3D(context, skin, BodyPart.RightLeg, new Vector3D(2, 12, 0), isSlim, yaw, walkAngle);
                DrawPart3D(context, skin, BodyPart.Torso, new Vector3D(0, 0, 0), isSlim, yaw, 0);
                DrawPart3D(context, skin, BodyPart.LeftArm, new Vector3D(isSlim ? -5.5 : -6, 0, 0), isSlim, yaw, walkAngle);
                DrawPart3D(context, skin, BodyPart.RightArm, new Vector3D(isSlim ? 5.5 : 6, 0, 0), isSlim, yaw, -walkAngle);
                DrawPart3D(context, skin, BodyPart.Head, new Vector3D(0, -8, 0), isSlim, yaw, 0);
            }
        }

        private enum BodyPart { Head, Torso, RightArm, LeftArm, RightLeg, LeftLeg }
        private struct Vector3D { public double X, Y, Z; public Vector3D(double x, double y, double z) { X = x; Y = y; Z = z; } }
        private struct PartSize { public double W, H, D; public PartSize(double w, double h, double d = 0) { W = w; H = h; D = d == 0 ? w : d; } }

        private static void DrawPart3D(DrawingContext context, Bitmap skin, BodyPart part, Vector3D pos, bool isSlim, double yaw, double rotX)
        {
            Rect f, s, t; PartSize size; bool h2 = skin.Size.Height >= 64;
            switch (part) {
                case BodyPart.Head:
                    f = new Rect(8, 8, 8, 8); s = new Rect(0, 8, 8, 8); t = new Rect(8, 0, 8, 8); size = new PartSize(8, 8, 8);
                    DrawCube(context, skin, pos, size, f, s, t, new Rect(40, 8, 8, 8), new Rect(32, 8, 8, 8), new Rect(40, 0, 8, 8), yaw, rotX); break;
                case BodyPart.Torso:
                    f = new Rect(20, 20, 8, 12); s = new Rect(16, 20, 4, 12); t = new Rect(20, 16, 8, 4); size = new PartSize(8, 12, 4);
                    DrawCube(context, skin, pos, size, f, s, t, h2?new Rect(20, 36, 8, 12):null, h2?new Rect(16, 36, 4, 12):null, h2?new Rect(20, 32, 8, 4):null, yaw, rotX); break;
                case BodyPart.RightArm:
                    int w = isSlim?3:4; f = new Rect(44, 20, w, 12); s = new Rect(40, 20, 4, 12); t = new Rect(44, 16, w, 4); size = new PartSize(w, 12, 4);
                    DrawCube(context, skin, pos, size, f, s, t, h2?new Rect(44, 36, w, 12):null, h2?new Rect(40, 36, 4, 12):null, h2?new Rect(44, 32, w, 4):null, yaw, rotX); break;
                case BodyPart.LeftArm:
                    w = isSlim?3:4;
                    if(h2){ f=new Rect(36, 52, w, 12); s=new Rect(32, 52, 4, 12); t=new Rect(36, 48, w, 4); 
                        DrawCube(context, skin, pos, new PartSize(w, 12, 4), f, s, t, new Rect(52, 52, w, 12), new Rect(48, 52, 4, 12), new Rect(52, 48, w, 4), yaw, rotX);
                    } else { f=new Rect(44, 20, w, 12); s=new Rect(40, 20, 4, 12); t=new Rect(44, 16, w, 4); 
                        DrawCube(context, skin, pos, new PartSize(w, 12, 4), f, s, t, null, null, null, yaw, rotX); } break;
                case BodyPart.RightLeg:
                    f = new Rect(4, 20, 4, 12); s = new Rect(0, 20, 4, 12); t = new Rect(4, 16, 4, 4); size = new PartSize(4, 12, 4);
                    DrawCube(context, skin, pos, size, f, s, t, h2?new Rect(4, 36, 4, 12):null, h2?new Rect(0, 36, 4, 12):null, h2?new Rect(4, 32, 4, 4):null, yaw, rotX); break;
                case BodyPart.LeftLeg:
                    if(h2){ f=new Rect(20, 52, 4, 12); s=new Rect(16, 52, 4, 12); t=new Rect(20, 48, 4, 4);
                        DrawCube(context, skin, pos, new PartSize(4, 12, 4), f, s, t, new Rect(4, 52, 4, 12), new Rect(0, 52, 4, 12), new Rect(4, 48, 4, 4), yaw, rotX);
                    } else { f=new Rect(4, 20, 4, 12); s=new Rect(0, 20, 4, 12); t=new Rect(4, 16, 4, 4);
                        DrawCube(context, skin, pos, new PartSize(4, 12, 4), f, s, t, null, null, null, yaw, rotX); } break;
            }
        }

        private static void DrawCube(DrawingContext context, Bitmap skin, Vector3D pos, PartSize size, 
            Rect f, Rect s, Rect t, Rect? f2, Rect? s2, Rect? t2, double yaw, double rotX)
        {
            double sc = Scale;
            double cosY = Math.Cos(yaw); double sinY = Math.Sin(yaw);
            double cosX = Math.Cos(rotX); double sinX = Math.Sin(rotX);
            double cos30 = Math.Cos(Math.PI / 6); double sin30 = Math.Sin(Math.PI / 6);

            Func<double, double, double, Point> project = (x, y, z) => {
                double rx = x;
                double ry = y * cosX - z * sinX;
                double rz = y * sinX + z * cosX;

                double wx = rx + pos.X;
                double wy = ry + pos.Y;
                double wz = rz + pos.Z;

                double finalX = wx * cosY - wz * sinY;
                double finalY = wy;
                double finalZ = wx * sinY + wz * cosY;

                return new Point((finalX - finalZ) * cos30 * sc, ((finalX + finalZ) * sin30 + finalY) * sc);
            };

            Action<Rect, Rect?, Point, Point, Point> drawFace = (src, src2, pA, pB, pC) => {
                var m = new Matrix((pB.X - pA.X) / src.Width, (pB.Y - pA.Y) / src.Width,
                                   (pC.X - pA.X) / src.Height, (pC.Y - pA.Y) / src.Height, pA.X, pA.Y);
                using (context.PushTransform(m)) {
                    context.DrawImage(skin, src, new Rect(0, 0, src.Width, src.Height));
                    if (src2.HasValue) context.DrawImage(skin, src2.Value, new Rect(0, 0, src.Width, src.Height));
                }
            };

            double hw = size.W / 2; double hd = size.D / 2;
            drawFace(t, t2, project(-hw, 0, -hd), project(hw, 0, -hd), project(-hw, 0, hd));
            drawFace(f, f2, project(-hw, 0, hd), project(hw, 0, hd), project(-hw, size.H, hd));
            drawFace(s, s2, project(hw, 0, hd), project(hw, 0, -hd), project(hw, size.H, hd));
            
            // 侧面阴影
            var pA = project(hw, 0, hd);
            var pB = project(hw, 0, -hd);
            var pC = project(hw, size.H, hd);
            if (Math.Abs(pB.X - pA.X) > 0.1) {
                var sideM = new Matrix((pB.X - pA.X) / (size.D), (pB.Y - pA.Y) / (size.D), (pC.X - pA.X) / (size.H), (pC.Y - pA.Y) / (size.H), pA.X, pA.Y);
                using (context.PushTransform(sideM)) {
                    context.FillRectangle(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), new Rect(0, 0, size.D, size.H));
                }
            }
        }

        private static void DrawPart(DrawingContext context, Bitmap source, Rect src, Rect dst) => context.DrawImage(source, src, dst);
    }
}
