using Nolock.social.MistralOcr.IntegrationTests.Fixtures;
using Nolock.social.MistralOcr.IntegrationTests.Helpers;

namespace Nolock.social.MistralOcr.IntegrationTests;

public abstract class TestBase : IClassFixture<MistralOcrTestFixture>
{
    protected readonly MistralOcrTestFixture Fixture;

    protected TestBase(MistralOcrTestFixture fixture)
    {
        Fixture = fixture;
    }

    protected static string GetTestImageUrl()
    {
        // Use a simple test image that should be accessible
        return "https://raw.githubusercontent.com/mistralai/mistral-common/main/tests/resources/dog.jpg";
    }

    protected static string GetTestImageDataUrl()
    {
        // A small 1x1 red pixel PNG as base64
        return "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
    }
    
    protected static string GetTestImageDataUrlWithText()
    {
        // A simple test image with "TEST" text - 100x50 PNG
        return "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAGQAAAAyCAYAAACqNX6+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAKqSURBVHhe7ZtBjtswDEVz/0v0Ap1FFl10gN6giy4GXaBAN93kCL1EgV6iQC/RoUXJomRKMmXJI8uT+R94MBLHsET+T7Ljj5hzznkBjMFkMplMJpPJZDKZTCaTyWQymUwmk8lkMplMJv9bcXd3973RaDyTJPm9ubl5f3V19e7q6urh8vLy2+Xl5Y+Li4sfFxcXP8/Pz39dXFz8vri4+Hl+fv7r/Pz898XFxZ+Li4s/5+fnf87Ozv6cn5//OT09/XN6evr79PT09+np6a+Tk5Nfp6env05OTn6dnJz8Ojk5+Xl8fPzz+Pj458nJyY/j4+MfJycnP46Ojn4cHR39ODw8/HF4ePjj8PDwx8HBwff9/f3vBwcH3/f397/t7+9/29vb+7a3t/dtd3f3287OzreNjY1v6+vrn1dXV9/u7u7+AX1OOOfc5eXlD6Lg169fXzSbzef7+/sPV1dX77a2tt6sr6+/WltbeyFJ8nRlZeVxaWnpYXFx8X5hYeFuYWHhZn5+/np+fv5qbm7uYnZ29nx2dvZsdnb2dGZm5mRmZuZoZmbmcHp6+mB6enp/amrqy9TU1O7U1NTO5OTkp8nJyY+Tk5MfJiYm3k9MTLybmJh4OzEx8WZ8fPz1+Pj4q7GxsZdjY2Mvx8bGXoyOjj4fHR19Pjo6+nxkZOTZyMjI05GRkScjIyOPh4eHHw8PDz8aHh5+ODQ09GBoaOjB0NDQ/cHBwfuDg4P3BgYG7g0MDNwdGBi40d/ffzMwMHBzY2Pj3/X19b/r6+t/19bW/q6trf1dXV39u7q6+nd1dfXPysrKn5WVlT/Ly8t/lpeX/ywtLf1eWlr6vbS09HtxcfH34uLi78XFxV8LCwu/FhYWfi0sLPycn5//OT8//3Nubu7n3Nzcz7m5uR+zs7M/Zmdnv8/MzHyfmZn5PjMz831qaupbT09P4r/ZGnPOOX8Bc7FME5nH+ysAAAAASUVORK5CYII=";
    }

    protected static byte[] GetTestImageBytes()
    {
        // Use a real receipt image from embedded resources
        return TestImageHelper.GetReceiptImageBytes(1);
    }
    
    protected static Stream GetTestImageStream(int receiptNumber = 1)
    {
        return TestImageHelper.GetReceiptImageStream(receiptNumber);
    }
    
    protected static string GetRealReceiptDataUrl(int receiptNumber = 1)
    {
        return TestImageHelper.GetReceiptImageDataUrl(receiptNumber);
    }
}