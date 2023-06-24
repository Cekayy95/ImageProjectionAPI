using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
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
        byte[] imageData = Convert.FromBase64String(processImageInput.ImageData);
        var matrixData = processImageInput.TransformMatrix.Split(";").Select(x => float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        Matrix4x4 transformMatrix = 
            new Matrix4x4(matrixData[0], matrixData[1], matrixData[2], matrixData[3],
        matrixData[4], matrixData[5], matrixData[6], matrixData[7],
        matrixData[8], matrixData[9], matrixData[10], matrixData[11],
        matrixData[12], matrixData[13], matrixData[14], matrixData[15]);

        Image imag = Image.Load(imageData);
        ProjectiveTransformBuilder projectiveTransformBuilder = new();
        projectiveTransformBuilder.AppendMatrix(transformMatrix);
        imag.Mutate(ctx =>
        {
            ctx.Resize(1240, 1754)
                .Transform(projectiveTransformBuilder, KnownResamplers.Welch);
            if(imag.Size.Width > 1240 && imag.Size.Height > 1754 )
                ctx.Crop(new Rectangle(0, 0, 1240, 1754));
        });
        string result = imag.ToBase64String(PngFormat.Instance).Replace("data:image/png;base64,", "");
        return result;
    })
.WithName("processImage")
.WithOpenApi();

app.Run();

public class ImageInput
{
    public string ImageData { get; set; }
    public string TransformMatrix { get; set; }
}