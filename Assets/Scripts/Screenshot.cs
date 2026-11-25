using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
using NUnit.Framework;

public class Screenshot : MonoBehaviour
{
    [SerializeField] private string gameName = "Awesome Game";
    [SerializeField] private RawImage showImage;
    private byte[] currentTexture;
    private string currentFilePath;

    [SerializeField] private GameObject screenShotPanel;
    [SerializeField] private GameObject capturePanel;
    [SerializeField] private GameObject saveImagePanel;

    public string ScreenShotName()
    {
        return string.Format("{0}_{1}.png", gameName, System.DateTime.Now.ToString("yyyy-MM-dd_HH--mm--ss"));
    }

    public void Capture()
    {
        StartCoroutine(TakeScreenShotAndShare());
    }

    private IEnumerator TakeScreenShotAndShare()
    {
        HideUIForCapture();

        yield return new WaitForEndOfFrame();

        Texture2D screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenShot.Apply();

        currentFilePath = Path.Combine(Application.temporaryCachePath, "screenshot_img.png");
        currentTexture = screenShot.EncodeToPNG();
        File.WriteAllBytes(currentFilePath, screenShot.EncodeToPNG());

        ShowImage();
        ShowUIForCapture();

        // To avoid memory leaks
        Destroy(screenShot);
    }

    private void HideUIForCapture()
    {
        capturePanel.SetActive(false);
    }

    private void ShowUIForCapture()
    {
        capturePanel.SetActive(true);
    }

    public void ShowImage()
    {
        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        texture.LoadImage(currentTexture);
        showImage.material.mainTexture = texture;
        screenShotPanel.SetActive(true);
    }

    public void ShareImage()
    {
        new NativeShare().AddFile(currentFilePath)
            .SetSubject("Subject goes here").SetText("Hello World").SetUrl("https://github.com/yasirkula/UnityNativeShare")
            .SetCallback((result, shareTarget) => Debug.Log($"Share result: {result}, select app: {shareTarget}"))
            .Share();
    }

    public async void SaveToGallery()
    {
        NativeGallery.Permission permission = await NativeGallery.RequestPermissionAsync(
            NativeGallery.PermissionType.Write,
            NativeGallery.MediaType.Image);
        Debug.Log($"Permission result: {permission}");

        if (permission != NativeGallery.Permission.Granted)
        {
            return;
        }

        NativeGallery.SaveImageToGallery(currentFilePath, gameName, ScreenShotName(),
            (success, path) =>
            {
                Debug.Log($"Media save result: {success} {path}");
                if (success)
                {
                    saveImagePanel.SetActive(true);

                    string editorFilePath = Path.Combine(Application.persistentDataPath, ScreenShotName());
                    File.WriteAllBytes(editorFilePath, currentTexture);
                }
            });
    }
}
