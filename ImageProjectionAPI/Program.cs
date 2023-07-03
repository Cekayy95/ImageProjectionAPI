using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/processImage", ([FromBody] ImageInput processImageInput) =>
    {
        var sw = Stopwatch.StartNew();
        byte[] imageData = Convert.FromBase64String(processImageInput.ImageData);
        if (processImageInput.TransformMatrix.Contains(","))
            processImageInput.TransformMatrix = processImageInput.TransformMatrix.Replace(",", ".");
        var matrixData = processImageInput.TransformMatrix.Split(";").Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        Matrix4x4 transformMatrix =
            new Matrix4x4(matrixData[0], matrixData[1], matrixData[2], matrixData[3],
        matrixData[4], matrixData[5], matrixData[6], matrixData[7],
        matrixData[8], matrixData[9], matrixData[10], matrixData[11],
        matrixData[12], matrixData[13], matrixData[14], matrixData[15]);
        Image imag = Image.Load(imageData);
        ImageLoadHelper.RotateImageFromExifData(imag).GetAwaiter().GetResult();
        imag.Mutate(ctx => ctx.Resize(1240, 1754));
        ProjectiveTransformBuilder projectiveTransformBuilder = new();
        projectiveTransformBuilder.AppendMatrix(transformMatrix);
        imag.Mutate(ctx =>
        {
            ctx.Transform(projectiveTransformBuilder, KnownResamplers.Welch);
        });
        if (imag.Size.Width > 1240 && imag.Size.Height > 1754)
            imag.Mutate(x =>
        {
            x.Crop(new Rectangle(0, 0, 1240, 1754));
        });

        string result = imag.ToBase64String(PngFormat.Instance).Replace("data:image/png;base64,", "");
        imag.Dispose();
        sw.Stop();
        Debug.WriteLine($"{sw.Elapsed.Seconds}.{sw.Elapsed.Milliseconds}");
        return result;
    })
.WithName("processImage")
.WithOpenApi();

app.MapPost("/processImageBrightness", ([FromBody] ImageInputBrightness processImageInput) =>
    {
        byte[] imageData = Convert.FromBase64String(processImageInput.ImageData);

        Image imag = Image.Load(imageData);
        imag.Mutate(x => x.Brightness(float.Parse(processImageInput.BrightnessFactor, CultureInfo.InvariantCulture)));

        string result = imag.ToBase64String(PngFormat.Instance).Replace("data:image/png;base64,", "");
        return result;
    })
    .WithName("processImageBrightness")
    .WithOpenApi();

app.Run();

public class ImageInput
{
    public string ImageData { get; set; }
    public string TransformMatrix { get; set; }
}

public class ImageInputBrightness
{
    public string ImageData { get; set; }
    public string BrightnessFactor { get; set; }
}

public static class ImageLoadHelper
{
    public static async Task RotateImageFromExifData(Image image)
    {
        var orientation = image?.Metadata?.ExifProfile?.Values.FirstOrDefault(x => x.Tag == ExifTag.Orientation);
        //https://stackoverflow.com/questions/74948926/wrong-image-width-and-height-when-read-it-with-c-sharp
        if (orientation is null)
            return;
        var orientationExif = (ushort)orientation.GetValue()!;
        //Weil wir das Bild an dieser Stelle Richtig Rotieren, sollten wir die Orientation aus den Exif daten löschen,
        //da sonst beim Laden das Bild Wieder rotiert wird.

        //TODO: prüfen ob das sinnig ist, oder doch das Bild wieder mit der Original Rotation speichern?!
        image?.Metadata?.ExifProfile?.SetValue(ExifTag.Orientation, (ushort)1); /*RemoveValue(ExifTag.Orientation);*/
        switch (orientationExif)
        {
            case 2:
                image.Mutate(img => img.RotateFlip(RotateMode.None, FlipMode.Horizontal));
                break;
            case 3:
                image.Mutate(img => img.RotateFlip(RotateMode.Rotate180, FlipMode.None));
                break;
            case 4:
                image.Mutate(img => img.RotateFlip(RotateMode.None, FlipMode.Vertical));
                break;
            case 5:
                image.Mutate(img => img.RotateFlip(RotateMode.Rotate270, FlipMode.Horizontal));
                break;
            case 6:
                image.Mutate(img => img.RotateFlip(RotateMode.Rotate90, FlipMode.None));
                break;
            case 7:
                image.Mutate(img => img.RotateFlip(RotateMode.Rotate90, FlipMode.Horizontal));
                break;
            case 8:
                image.Mutate(img => img.RotateFlip(RotateMode.Rotate270, FlipMode.None));
                break;
            case 1:
            default:
                break;
        }

    }
}