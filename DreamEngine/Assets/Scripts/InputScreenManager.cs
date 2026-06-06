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
    private string selectedImageStyle = "Super Surreal";

    [Header("Chapter Screen")]
    public GameObject chapterPanel;
    public TextMeshProUGUI chapterText;
    public RawImage chapterImage;
    public TextMeshProUGUI redrawButtonText;

    private string proxyUrl = "http://127.0.0.1:5001";
    private string currentDream;
    private string currentObstacles;
    private string currentStory = "";

    private int dreamProgress = 0;
    private int stability = 5;
    private int fear = 3;
    private int integrity = 5;
    private int obsession = 0;
    private const int maxTurns = 3;
    private int turnCount = 0;
    private const int turnsBeforeFinalObstacle = 2;
    private bool finalObstacleShown = false;
    private bool gameEnded = false;
    public GameObject choiceButtonRow;
    private string selectedDreamStyle = "Super Surreal";
    private string redrawDefaultText = "Re-draw Image";

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;

    void Start()
    {
        titleText.text = "DREAM ENGINE";
        inputPanel.SetActive(true);
        chapterPanel.SetActive(false);
    }

    public void OnStartButtonClicked()
    {
        Debug.Log("Button clicked!");
        Debug.Log("Image Style = " + selectedImageStyle);
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

    public void OnImageStyleChanged(int selectedIndex)
    {
        switch (selectedIndex)
        {
            case 0:
                selectedDreamStyle = "Super Surreal";
                break;
            case 1:
                selectedDreamStyle = "Surreal Funny";
                break;
            case 2:
                selectedDreamStyle = "Surreal Dark";
                break;
            default:
                selectedDreamStyle = "Super Surreal";
                break;
        }

        Debug.Log("Dream style selected: " + selectedDreamStyle);
    }

    private string GetStoryTonePrompt()
    {
        switch (selectedDreamStyle)
        {
            case "Surreal Funny":
                return "Use a surreal, absurd, darkly funny tone with bizarre humor and strange dream logic.";

            case "Surreal Dark":
                return "Use a dark surreal tone with eerie atmosphere, emotional heaviness, psychological tension, and haunting imagery.";

            case "Super Surreal":
            default:
                return "Use a highly surreal, symbolic, dreamlike tone with uncanny beauty, mystery, and imaginative strangeness.";
        }
    }

    private string GetImageStylePrompt()
    {
        switch (selectedDreamStyle)
        {
            case "Surreal Funny":
                return "Use surreal humor, absurd visual details, strange comedic imagery, and bizarre dream logic.";

            case "Surreal Dark":
                return "Use a dark surreal visual style with eerie atmosphere, haunting symbolism, ominous mood, and unsettling dream imagery.";

            case "Super Surreal":
            default:
                return "Use a highly surreal visual style with symbolic imagery, dreamlike distortions, uncanny beauty, and imaginative strangeness.";
        }
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
                             "Write a short dramatic opening chapter event of 2-3 sentences, under 75 words total. " + $"{GetStoryTonePrompt()} " + 
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
                currentStory = storyResponse.text;

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
        StartCoroutine(GenerateChapterImage(currentStory));

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

    public void OnChaseDreamClicked()
    {
        dreamProgress += 2;
        obsession += 1;
        stability -= 1;

        ProcessChoice("Chase Dream");
    }

    public void OnPreserveSelfClicked()
    {
        stability += 2;
        fear -= 1;

        ProcessChoice("Preserve Self");
    }

    public void OnEscapeNightmareClicked()
    {
        fear -= 2;
        dreamProgress -= 1;
        integrity -= 1;

        ProcessChoice("Escape Nightmare");
    }

    private void LogCurrentStats()
    {
        Debug.Log(
            $"Stats — Dream Progress: {dreamProgress}, " +
            $"Stability: {stability}, " +
            $"Fear: {fear}, " +
            $"Integrity: {integrity}, " +
            $"Obsession: {obsession}"
        );
    }

    private void ProcessChoice(string chosenAction)
    {
        if (gameEnded)
        {
            return;
        }

        ClampStats();
        LogCurrentStats();

        Debug.Log("Player chose: " + chosenAction);

        // The player is responding to the cliffhanger.
        // Their next result should be the ending.
        if (finalObstacleShown)
        {
            StartCoroutine(GenerateEnding(chosenAction));
            return;
        }

        turnCount++;

        // After two normal turns, generate the major final obstacle.
        if (turnCount >= turnsBeforeFinalObstacle)
        {
            StartCoroutine(GenerateFinalObstacle(chosenAction));
        }
        else
        {
            StartCoroutine(GenerateNextChapter(chosenAction));
        }
    }

    IEnumerator GenerateNextChapter(string chosenAction)
    {
        chapterText.text = "Your dream shifts...";

        string nextPrompt =
            $"The player's original dream is: {currentDream}. " +
            $"{GetStoryTonePrompt()} " +
            $"The obstacles are: {currentObstacles}. " +
            $"The previous chapter was: {currentStory}. " +
            $"The player chose this action: {chosenAction}. " +
            $"Current stats: dream progress {dreamProgress}, stability {stability}, " +
            $"fear {fear}, integrity {integrity}, obsession {obsession}. " +
            "Write the next dramatic chapter event in 2-3 sentences, under 75 words total. " +
            "Make the consequences of the choice clear. " +
            "Use second-person perspective. No headers or titles.";

        string storyJson =
            JsonUtility.ToJson(new PromptRequest { prompt = nextPrompt });

        UnityWebRequest storyRequest =
            UnityWebRequest.Post(proxyUrl + "/claude", storyJson, "application/json");

        yield return storyRequest.SendWebRequest();

        if (storyRequest.result == UnityWebRequest.Result.Success)
        {
            StoryResponse storyResponse =
                JsonUtility.FromJson<StoryResponse>(
                    storyRequest.downloadHandler.text
                );

            currentStory = storyResponse.text;
            chapterText.text = currentStory;
            Debug.Log("Generating new scene image for the next chapter...");
            StartCoroutine(GenerateChapterImage(currentStory));

            Debug.Log("Next chapter: " + currentStory);
        }
        else
        {
            chapterText.text = "The dream fractures unexpectedly.";
            Debug.LogError("Next chapter error: " + storyRequest.error);
        }
    }

    IEnumerator GenerateFinalObstacle(string chosenAction)
    {
        chapterText.text = "Something stands between you and the dream...";

        string obstaclePrompt =
            $"The player's original dream is: {currentDream}. " +
            $"{GetStoryTonePrompt()} " + 
            $"Their original obstacles are: {currentObstacles}. " +
            $"The previous chapter was: {currentStory}. " +
            $"Their most recent action was: {chosenAction}. " +
            $"Current stats: dream progress {dreamProgress}, stability {stability}, " +
            $"fear {fear}, integrity {integrity}, obsession {obsession}. " +
            "Generate a dramatic final obstacle or cliffhanger before the ending. " +
            "The obstacle should force a painful tradeoff between pursuing the dream, " +
            "preserving the player's well-being, or escaping the nightmare. " +
            "Write 2-3 emotionally specific sentences in second-person perspective. " +
            "Keep the response under 85 words total." + 
            "Do not resolve the obstacle. End with a tense decision point. No headers.";

        string storyJson =
            JsonUtility.ToJson(new PromptRequest { prompt = obstaclePrompt });

        UnityWebRequest storyRequest =
            UnityWebRequest.Post(proxyUrl + "/claude", storyJson, "application/json");

        yield return storyRequest.SendWebRequest();

        if (storyRequest.result == UnityWebRequest.Result.Success)
        {
            StoryResponse storyResponse =
                JsonUtility.FromJson<StoryResponse>(
                    storyRequest.downloadHandler.text
                );

            currentStory = storyResponse.text;
            chapterText.text = currentStory;
            Debug.Log("Generating new scene image for final obstacle...");
            StartCoroutine(GenerateChapterImage(currentStory));

            finalObstacleShown = true;

            Debug.Log("Final obstacle: " + currentStory);
        }
        else
        {
            chapterText.text =
                "The path fractures beneath you. One last decision remains.";

            finalObstacleShown = true;

            Debug.LogError("Final obstacle error: " + storyRequest.error);
        }
    }

    private string DetermineEndingType()
    {
        if (dreamProgress >= 4 && stability >= 2)
        {
            return "hopeful success";
        }

        if (dreamProgress >= 4 && obsession >= 3)
        {
            return "dark success";
        }

        if (stability >= 6 && dreamProgress < 4)
        {
            return "peaceful release";
        }

        return "tragic or ambiguous ending";
    }

    IEnumerator GenerateEnding(string chosenAction)
    {
        string endingType = DetermineEndingType();

        chapterText.text = "Your fate is being decided...";

        string endingPrompt =
            $"The player's original dream is: {currentDream}. " +
            $"{GetStoryTonePrompt()} " + 
            $"The obstacles are: {currentObstacles}. " +
            $"The previous chapter was: {currentStory}. " +
            $"Their final action was: {chosenAction}. " +
            $"Final stats: dream progress {dreamProgress}, stability {stability}, " +
            $"fear {fear}, integrity {integrity}, obsession {obsession}. " +
            $"Write a short {endingType} ending in 3-4 sentences, under 100 words total. " +
            "Make it emotionally specific. Explain what the player gained and what it cost them. " +
            "Use second-person perspective. No headers.";

        string storyJson =
            JsonUtility.ToJson(new PromptRequest { prompt = endingPrompt });

        UnityWebRequest storyRequest =
            UnityWebRequest.Post(proxyUrl + "/claude", storyJson, "application/json");

        yield return storyRequest.SendWebRequest();

        if (storyRequest.result == UnityWebRequest.Result.Success)
        {
            StoryResponse storyResponse =
                JsonUtility.FromJson<StoryResponse>(
                    storyRequest.downloadHandler.text
                );

            currentStory = storyResponse.text;
            chapterText.text = currentStory;
            Debug.Log("Generating new scene image for ending...");
            StartCoroutine(GenerateChapterImage(currentStory));

            Debug.Log("Ending: " + currentStory);
        }
        else
        {
            chapterText.text = "The dream dissolves before you can understand what it meant.";
            Debug.LogError("Ending error: " + storyRequest.error);
        }

        gameEnded = true;
        choiceButtonRow.SetActive(false);
        gameOverPanel.SetActive(true);
        gameOverText.text = "DREAM ENDS";
    }

    public void OnReplayClicked()
    {
        turnCount = 0;
        finalObstacleShown = false;
        gameEnded = false;

        dreamProgress = 0;
        stability = 5;
        fear = 3;
        integrity = 5;
        obsession = 0;

        currentDream = "";
        currentObstacles = "";
        currentStory = "";

        dreamInputField.text = "";
        obstaclesInputField.text = "";

        chapterText.text = "";
        chapterImage.texture = null;

        choiceButtonRow.SetActive(true);
        gameOverPanel.SetActive(false);

        chapterPanel.SetActive(false);
        inputPanel.SetActive(true);

        Debug.Log("Game reset.");
    }



    private void ClampStats()
    {
        fear = Mathf.Max(0, fear);
        dreamProgress = Mathf.Max(0, dreamProgress);
        stability = Mathf.Max(0, stability);
        integrity = Mathf.Max(0, integrity);
        obsession = Mathf.Max(0, obsession);
    }

    IEnumerator GenerateChapterImage(string storyContext)
    {
        Debug.Log("Generating image for current chapter...");

        string imagePrompt =
    $"Create a cinematic digital art scene inspired by this chapter: {storyContext}. " +
    $"The player's dream is: {currentDream}. " +
    $"The obstacles are: {currentObstacles}. " +
    $"{GetImageStylePrompt()} " +
    "Focus on atmosphere, symbolism, environment, and mood. " +
    "Avoid showing a specific person unless necessary. " +
    "If a person appears, show only a distant ambiguous silhouette.";

        string imageJson =
            JsonUtility.ToJson(new PromptRequest { prompt = imagePrompt });

        byte[] imageBytes =
            System.Text.Encoding.UTF8.GetBytes(imageJson);

        UnityWebRequest imageRequest =
            new UnityWebRequest(proxyUrl + "/image", "POST");

        imageRequest.uploadHandler =
            new UploadHandlerRaw(imageBytes);

        imageRequest.downloadHandler =
            new DownloadHandlerBuffer();

        imageRequest.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        yield return imageRequest.SendWebRequest();

        if (imageRequest.result == UnityWebRequest.Result.Success)
        {
            ImageResponse parsedImage =
                JsonUtility.FromJson<ImageResponse>(
                    imageRequest.downloadHandler.text
                );

            Debug.Log("Image path: " + parsedImage.image_path);

            LoadImageFromDisk(parsedImage.image_path);
            redrawButtonText.text = redrawDefaultText;
        }
        else
        {
            Debug.LogError(
                "Image generation error: " + imageRequest.error
            );
            redrawButtonText.text = redrawDefaultText;
        }
    }

    private void GenerateImageForCurrentStory()
    {
        StartCoroutine(GenerateChapterImage(currentStory));
    }

    public void OnRedrawImageClicked()
    {
        if (string.IsNullOrEmpty(currentStory))
        {
            Debug.Log("No story available to redraw.");
            return;
        }

        redrawButtonText.text = "Rendering another dream fragment...";

        Debug.Log("Re-drawing current chapter image...");
        StartCoroutine(GenerateChapterImage(currentStory));
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