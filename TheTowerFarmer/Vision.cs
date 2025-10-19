using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.Versioning;
using Tesseract;
using Rect = OpenCvSharp.Rect;

namespace TheTowerFarmer;

internal class Vision
{
    private TesseractEngine _ocrEngine = new TesseractEngine("", "eng", EngineMode.LstmOnly);
    public Vision()
    {
        _ocrEngine.SetVariable("tessedit_char_whitelist", "0123456789.$/%abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
    }

    public Point? FindTemplate(Mat source, string templatePath, double threshold = 0.8, bool center = true)
    {
        using var template = Cv2.ImRead(templatePath, ImreadModes.Color);
        using var result = new Mat();

        Cv2.MatchTemplate(source, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal < threshold)
            return null;

        if (center)
        {
            var centerX = maxLoc.X + template.Width / 2.0;
            var centerY = maxLoc.Y + template.Height / 2.0;
            return new Point((int)centerX, (int)centerY);
        }

        return new Point(maxLoc.X, maxLoc.Y);
    }

    public Point? DetectGemByColor(Mat frame, Point center, int orbitRadius)
    {
        var cropRect = new Rect(center.X - orbitRadius, center.Y - orbitRadius, orbitRadius * 2, orbitRadius * 2);
        using var cropped = new Mat(frame, cropRect);

        using var hsv = cropped.CvtColor(ColorConversionCodes.BGR2HSV);

        var lowerPurple = new Scalar(120, 60, 100);
        var upperPurple = new Scalar(160, 255, 255);

        using var colorMask = hsv.InRange(lowerPurple, upperPurple);

        Cv2.FindContours(colorMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        double bestScore = 0;
        double bestArea = 0;
        Point? bestPoint = null;

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < 2000 || area > 3000) 
                continue;
            
            var rect = Cv2.BoundingRect(contour);
            var gemCenter = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            double dx = gemCenter.X - center.X;
            double dy = gemCenter.Y - center.Y;

            double dist = Math.Sqrt(dx * dx + dy * dy);
            var orbitError = Math.Abs(dist - orbitRadius);
            double score = area / (1 + orbitError);

            var gemCenterFull = new Point(
                cropRect.X + gemCenter.X,
                cropRect.Y + gemCenter.Y
            );

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = gemCenterFull;
                bestArea = area;
            }
        }
        
        return bestPoint;
    }

    public List<(string Name, string Value)> DetectUpgrades(Mat frame)
    {
        var boxes = new List<Mat>();
        var output = new List<(string Name, string Value)>();

        using var gray = frame.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var blurred = gray.GaussianBlur(new Size(3, 3), 0);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 60, 150);

        using var dilated = new Mat();
        Cv2.Dilate(edges, dilated, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3)));

        Cv2.FindContours(dilated, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var peri = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);

            if (approx.Length == 4 && Cv2.ContourArea(approx) > 60000)
            {
                var rect = Cv2.BoundingRect(approx);

                double aspect = (double)rect.Width / rect.Height;
                if (aspect > 1.5 && aspect < 3.5)
                {
                    boxes.Add(new Mat(frame, rect));
                }
            }
        } 

        foreach (var box in boxes)
        {
            using var roiGray = box.CvtColor(ColorConversionCodes.BGR2GRAY);

            Cv2.Resize(roiGray, roiGray, new Size(), 2.0, 2.0, InterpolationFlags.Cubic);
            Cv2.Threshold(roiGray, roiGray, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            Cv2.BitwiseNot(roiGray, roiGray);

            // Remove the border
            var thickness = 16;
            Cv2.Rectangle(roiGray, new Rect(0, 0, roiGray.Cols, thickness), Scalar.White, -1);
            Cv2.Rectangle(roiGray, new Rect(0, roiGray.Rows - thickness, roiGray.Cols, thickness), Scalar.White, -1);
            Cv2.Rectangle(roiGray, new Rect(0, 0, thickness, roiGray.Rows), Scalar.White, -1);
            Cv2.Rectangle(roiGray, new Rect(roiGray.Cols - thickness, 0, thickness, roiGray.Rows), Scalar.White, -1);

            int midX = roiGray.Width / 2;
            using var leftHalf = new Mat(roiGray, new Rect(0, 0, midX, roiGray.Height));
            using var rightHalf = new Mat(roiGray, new Rect(midX, 0, roiGray.Width - midX, roiGray.Height));


#pragma warning disable CA1416
            using var leftPix = PixConverter.ToPix(leftHalf.ToBitmap());
            using var rightPix = PixConverter.ToPix(rightHalf.ToBitmap());
#pragma warning restore CA1416

            var name = "";
            var value = "";

            using (var page = _ocrEngine.Process(leftPix, PageSegMode.SingleColumn))
            {
                var text = page.GetText().Trim();
                name = text;
            }

            using (var page = _ocrEngine.Process(rightPix, PageSegMode.SingleBlock))
            {
                var text = page.GetText().Trim();
                value = text;
            }

            output.Add((name, value));
        }

        return output;
    }
}
