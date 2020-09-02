using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Application.Extensions
{
    public static class BitmapExtensions
    {
        public static void Save(this Bitmap bitmap, string fileName, long qualityLevel)
        {
            ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageDecoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);

            // Create an Encoder object based on the GUID  
            // for the Quality parameter category.  
            System.Drawing.Imaging.Encoder myEncoder =
                System.Drawing.Imaging.Encoder.Quality;

            // Create an EncoderParameters object.  
            // An EncoderParameters object has an array of EncoderParameter  
            // objects. In this case, there is only one  
            // EncoderParameter object in the array.  
            EncoderParameters myEncoderParameters = new EncoderParameters(1);

            var myEncoderParameter = new EncoderParameter(myEncoder, 65L);
            myEncoderParameters.Param[0] = myEncoderParameter;

            var fi = new FileInfo(fileName);
            if (!fi.Directory.Exists)
                fi.Directory.Create();
            bitmap.Save(fileName, jpgEncoder, myEncoderParameters);
        }
    }
}
