using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
/// <summary>
/// unity 异步gpu渲染图片读取
/// 这是一个示例，该示例显示了如何使用异步GPU回复API捕获渲染器而无需阻止主线程。
/// 请注意，性能和延迟之间存在权衡 - 仅当可以接受少量延迟时才有用。
/// 屏幕截图是该功能最适合的情况之一。
/// https://github.com/keijiro/AsyncCaptureTest/tree/master/Assets
/// </summary>
public sealed class AsyncCapture : MonoBehaviour
{
    (RenderTexture grab, RenderTexture flip) _rt;
    NativeArray<byte> _buffer;

    System.Collections.IEnumerator Start() {
        var (w, h) = (Screen.width, Screen.height);

        _rt.grab = new RenderTexture(w, h, 0);
        _rt.flip = new RenderTexture(w, h, 0);

        _buffer = new NativeArray<byte>(w * h * 4, Allocator.Persistent,
                                        NativeArrayOptions.UninitializedMemory);

        var (scale, offs) = (new Vector2(1, -1), new Vector2(0, 1));

        while (true) {
            yield return new WaitForSeconds(1);
            yield return new WaitForEndOfFrame();

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt.grab);
            Graphics.Blit(_rt.grab, _rt.flip, scale, offs);

            AsyncGPUReadback.RequestIntoNativeArray
              (ref _buffer, _rt.flip, 0, OnCompleteReadback);
        }
    }

    void OnDestroy() {
        AsyncGPUReadback.WaitAllRequests();

        Destroy(_rt.flip);
        Destroy(_rt.grab);

        _buffer.Dispose();
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request) {
        if (request.hasError) {
            Debug.Log("GPU readback error detected.");
            return;
        }

        using var encoded = ImageConversion.EncodeNativeArrayToPNG
          (_buffer, _rt.flip.graphicsFormat,
           (uint)_rt.flip.width, (uint)_rt.flip.height);

        System.IO.File.WriteAllBytes("test.png", encoded.ToArray());
    }
}