using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class InputScreenManager : MonoBehaviour
{
    [Header("Input Screen")]
    public TMP_InputField dreamInputField;
    public TMP_InputField obstaclesInputField;
    public TextMeshProUGUI titleText;
    public GameObject inputPanel;

    [Header("Chapter Screen")]
    public GameObject chapterPanel;
    public TextMeshProUGUI chapterText;
    public RawImage chapterImage;

    private string proxyUrl = "http://127.0.0.1:5001";
    private string currentDream;
    private string currentObstacles;

    void Start()
    {
        titleText.text = "DREAM ENGINE";
        inputPanel.SetActive(true);
        chapterPanel.SetActive(false);
    }

    public void OnStartButtonClicked()
    {
        Debug.Log("Button clicked!");
        currentDream = dreamInputField.text;
        currentObstacles = obstaclesInputField.text;
        Debug.Log("Dream: " + currentDream + " Obstacles: " + currentObstacles);

        if (string.IsNullOrEmpty(currentDream) || string.IsNullOrEmpty(currentObstacles))
        {
            Debug.Log("Please fill in both fields.");
            return;
        }

        Debug.Log("Starting coroutine...");
        StartCoroutine(GenerateChapter());
        Debug.Log("Coroutine started!");
    }

    IEnumerator GenerateChapter()
    {
        Debug.Log("GenerateChapter started!");
        Debug.Log("chapterPanel is null: " + (chapterPanel == null));
        Debug.Log("inputPanel is null: " + (inputPanel == null));
        // Switch to chapter screen immediately with loading text
        inputPanel.SetActive(false);
        chapterPanel.SetActive(true);
        chapterText.text = "Your dream is taking shape...";

        // --- CALL CLAUDE ---
        string storyPrompt = $"A person dreams of {currentDream} but faces these obstacles: {currentObstacles}. " +
                             "Write a short dramatic opening chapter event of 2-3 sentences. " +
                             "Make it emotional and specific. No headers or titles, just the story. Don't address the end-user by a specific name unless told to. Write in second person perspective.";

        string storyJson = JsonUtility.ToJson(new PromptRequest { prompt = storyPrompt });
        byte[] storyBytes = System.Text.Encoding.UTF8.GetBytes(storyJson);

        UnityWebRequest storyRequest = UnityWebRequest.Post(proxyUrl + "/claude", storyJson, "application/json");

        Debug.Log("Sending web request...");
        yield return storyRequest.SendWebRequest();
        Debug.Log("Web request returned! Result: " + storyRequest.result);
        Debug.Log("Response: " + storyRequest.downloadHandler.text);

        if (storyRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                Debug.Log("In Claude happy path...");
                string rawResponse = storyRequest.downloadHandler.text;
                StoryResponse storyResponse = JsonUtility.FromJson<StoryResponse>(rawResponse);
                chapterText.text = storyResponse.text;
                Debug.Log("Story text: " + chapterText.text);
                
            }
            catch (System.Exception e)
            {
                Debug.LogError("Parse error: " + e.Message);
                chapterText.text = "Your dream unfolds...";
            }
        }
        else
        {
            chapterText.text = "Something went wrong with the story.";
            Debug.LogError("Claude error: " + storyRequest.error);
        }

        // --- CALL REPLICATE ---
        Debug.Log("Starting Replicate call...");

        /*string imagePrompt =
     $"Create a surreal, cinematic digital art image inspired by the dream of {currentDream} " +
     $"and the obstacles of {currentObstacles}. " +
     "Show the dream through symbolic visual storytelling, environment, objects, and mood. " +
     "Prefer no person in the image. If a human figure must appear, show only an ambiguous silhouette or distant figure. " +
     "Avoid character portrait focus. Emotional, dark, beautiful, dreamlike atmosphere.";
        */
        string imagePrompt = $"{currentDream}, dramatic cinematic scene, surreal dream atmosphere, " +
            $"emotional, dark and beautiful, digital art";

        Debug.Log("Image prompt: " + imagePrompt);

        string imageJson = JsonUtility.ToJson(new PromptRequest { prompt = imagePrompt });
        byte[] imageBytes = System.Text.Encoding.UTF8.GetBytes(imageJson);

        UnityWebRequest imageRequest = new UnityWebRequest(proxyUrl + "/image", "POST");
        imageRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
        imageRequest.downloadHandler = new DownloadHandlerBuffer();
        imageRequest.SetRequestHeader("Content-Type", "application/json");

        yield return imageRequest.SendWebRequest();
        Debug.Log("Replicate response code: " + imageRequest.responseCode);
        Debug.Log("Replicate response: " + imageRequest.downloadHandler.text);

        if (imageRequest.result == UnityWebRequest.Result.Success)
        {
            string imageResponse = imageRequest.downloadHandler.text;
            ImageResponse parsedImage = JsonUtility.FromJson<ImageResponse>(imageResponse);

            Debug.Log("Image path: " + parsedImage.image_path);
            Debug.Log("Image url: " + parsedImage.image_url);

            LoadImageFromDisk(parsedImage.image_path);
        }
        else
        {
            Debug.LogError("Replicate error: " + imageRequest.error);
        }
    }

    IEnumerator LoadImage(string url)
    {
        UnityWebRequest imageLoader = UnityWebRequestTexture.GetTexture(url);
        yield return imageLoader.SendWebRequest();

        if (imageLoader.result == UnityWebRequest.Result.Success)
        {
            chapterImage.texture = DownloadHandlerTexture.GetContent(imageLoader);
        }
        else
        {
            Debug.LogError("Image load error: " + imageLoader.error);
        }
    }

    void LoadImageFromDisk(string path)
    {
        Debug.Log("Loading image from disk: " + path);

        byte[] imageBytes = System.IO.File.ReadAllBytes(path);

        Texture2D tex = new Texture2D(2, 2);
        bool loaded = tex.LoadImage(imageBytes);

        Debug.Log("Texture loaded from disk: " + loaded);

        if (loaded)
        {
            chapterImage.texture = tex;
            chapterImage.color = Color.white;
        }
        else
        {
            Debug.Log("There was a loading error.");
        }
    }

    [System.Serializable]
    public class PromptRequest
    {
        public string prompt;
    }

    [System.Serializable]
    public class StoryResponse
    {
        public string text;
    }

    [System.Serializable]
    public class ImageResponse
    {
        public string image_path;
        public string image_url;
    }

}