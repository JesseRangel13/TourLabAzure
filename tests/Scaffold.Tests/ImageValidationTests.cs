using TourLab.Azure.Catalog.Images;
using TourLab.Azure.Catalog.Application;
namespace TourLab.Azure.Tests;

public sealed class ImageValidationTests
{
    internal static byte[] Png=>Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    [Fact]
    public async Task DecoderRejectsCorruptionSignatureDimensionsOversizeAndCancellation()
    {
        await using var valid=await ValidatedImage.ReadAsync(new MemoryStream(Png),default);
        Assert.Equal("image/png",valid.ContentType);
        await using var jpeg=await ValidatedImage.ReadAsync(new MemoryStream(ImageFixtures.Jpeg),default);
        Assert.Equal("image/jpeg",jpeg.ContentType);
        var oversized=ImageFixtures.Jpeg;
        for(var i=0;i<oversized.Length-8;i++) if(oversized[i]==255&&oversized[i+1] is 0xc0 or 0xc2){oversized[i+7]=8;oversized[i+8]=1;break;}
        Assert.Equal("ImageDimensionsExceeded",(await Assert.ThrowsAsync<CatalogProblem>(()=>ValidatedImage.ReadAsync(new MemoryStream(oversized),default))).Code);
        var corrupt=Png;corrupt[40]^=1;
        Assert.Equal(400,(await Assert.ThrowsAsync<CatalogProblem>(()=>ValidatedImage.ReadAsync(new MemoryStream(corrupt),default))).Status);
        Assert.Equal(415,(await Assert.ThrowsAsync<CatalogProblem>(()=>ValidatedImage.ReadAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<svg><script>unsafe</script></svg>")),default))).Status);
        Assert.Equal(413,(await Assert.ThrowsAsync<CatalogProblem>(()=>ValidatedImage.ReadAsync(new MemoryStream(new byte[ValidatedImage.MaximumBytes+1]),default))).Status);
        using var stop=new CancellationTokenSource();stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ValidatedImage.ReadAsync(new MemoryStream(Png),stop.Token));
        var trailing=Png.Concat(new byte[]{0}).ToArray();
        Assert.Equal(400,(await Assert.ThrowsAsync<CatalogProblem>(()=>ValidatedImage.ReadAsync(new MemoryStream(trailing),default))).Status);
    }
    [Fact]
    public void StorageConfigurationRejectsPublicForeignOrProductionEmulatorEndpoints()
    {
        Assert.True(BlobImageStorage.Safe(new ImageStorageOptions(), false));
        Assert.True(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,ServiceUri="https://tourlabazureimages.blob.core.windows.net/" },false));
        Assert.False(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,ServiceUri="http://tourlabazureimages.blob.core.windows.net/" },false));
        Assert.False(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,ServiceUri="https://other-account.blob.core.windows.net/" },false));
        Assert.False(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,ServiceUri="https://tourlabazureimages.blob.core.windows.net/?sig=not-allowed" },false));
        Assert.False(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,Local=true,ConnectionString="UseDevelopmentStorage=true" },false));
        Assert.False(BlobImageStorage.Safe(new ImageStorageOptions { Enabled=true,Container="foreign",ServiceUri="https://tourlabazureimages.blob.core.windows.net/" },false));
    }}
