using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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
    public TextMeshProUGUI redrawButtonText;
    public CanvasGroup contentCanvasGroup;

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

    private List<ChapterRecord> chapterHistory = new List<ChapterRecord>();
    private int currentChapterIndex = -1;

    private string bookCreatedAt;
    private string bookDreamStyle;

    private bool isGenerating = false;

    private Coroutine loadingPulseCoroutine;
    public TextMeshProUGUI loadingText;

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
        Debug.Log("Image and Text Style = " + selectedDreamStyle);
        currentDream = dreamInputField.text;
        currentObstacles = obstaclesInputField.text;
        Debug.Log("Dream: " + currentDream + " Obstacles: " + currentObstacles);

        if (string.IsNullOrEmpty(currentDream) || string.IsNullOrEmpty(currentObstacles))
        {
            Debug.Log("Please fill in both fields.");
            return;
        }

        bookCreatedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        bookDreamStyle = selectedDreamStyle;

        Debug.Log("Starting coroutine...");
        isGenerating = true;
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
                RecordNewChapter("Opening", currentStory);

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
        StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));

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
        if (isGenerating) {

            Debug.Log("Image still generating...");
            return;
        }
            dreamProgress += 2;
            obsession += 1;
            stability -= 1;

            ProcessChoice("Chase Dream");
        
    }

    public void OnPreserveSelfClicked()
    {
        if (isGenerating)
        {

            Debug.Log("Image still generating...");
            return;
        }
        stability += 2;
        fear -= 1;

        ProcessChoice("Preserve Self");
    }

    public void OnEscapeNightmareClicked()
    {
        if (isGenerating)
        {

            Debug.Log("Image still generating...");
            return;
        }
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
        if (gameEnded || isGenerating)
        {
            Debug.Log("Please wait - the next chapter is still forming...");
            return;
        }

        isGenerating = true;

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
            RecordNewChapter("Chapter", currentStory, chosenAction);
            Debug.Log("Generating new scene image for the next chapter...");
            StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));

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
            RecordNewChapter("Final Obstacle", currentStory, chosenAction);
            Debug.Log("Generating new scene image for final obstacle...");
            StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));

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
            RecordNewChapter("Ending", currentStory, chosenAction);
            Debug.Log("Generating new scene image for ending...");
            StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));

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

        chapterHistory.Clear();
        currentChapterIndex = -1;
        bookCreatedAt = "";
        bookDreamStyle = "";

        Debug.Log("Game reset.");
    }

    private void RecordNewChapter(string chapterType,string text, string chosenAction = "")
    {
        ChapterRecord record = new ChapterRecord
        {
            chapterType = chapterType,
            chapterText = text,
            imagePath = "",
            chosenAction = chosenAction
        };

        chapterHistory.Add(record);
        currentChapterIndex = chapterHistory.Count - 1;

        Debug.Log(
            $"Recorded chapter {chapterHistory.Count}: {chapterType}"
        );
    }



    private void ClampStats()
    {
        fear = Mathf.Max(0, fear);
        dreamProgress = Mathf.Max(0, dreamProgress);
        stability = Mathf.Max(0, stability);
        integrity = Mathf.Max(0, integrity);
        obsession = Mathf.Max(0, obsession);
    }

    IEnumerator GenerateChapterImage(string storyContext, int chapterIndex)
    {
        Debug.Log("Generating image for current chapter...");
        ShowLoadingText();
        contentCanvasGroup.alpha = 0f;


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

        imageRequest.SetRequestHeader("Content-Type", "application/json");

        yield return imageRequest.SendWebRequest();

        if (imageRequest.result == UnityWebRequest.Result.Success)
        {
            ImageResponse parsedImage =
                JsonUtility.FromJson<ImageResponse>(
                    imageRequest.downloadHandler.text
                );

            LoadImageFromDisk(parsedImage.image_path);
            if (chapterIndex >= 0 && chapterIndex < chapterHistory.Count)
            {
                chapterHistory[chapterIndex].imagePath =
                    parsedImage.image_path;

                Debug.Log(
                    "Saved latest image for chapter: " +
                    chapterHistory[chapterIndex].chapterType
                );
            }
            if (gameEnded)
            {
                LogDreamBookHistory();
            }
            HideLoadingText();
            StartCoroutine(FadeInContent());
            redrawButtonText.text = redrawDefaultText;
            isGenerating = false;

        }
        else
        {
            Debug.LogError("Image generation error: " + imageRequest.error);
            HideLoadingText();
            contentCanvasGroup.alpha = 1f;
            redrawButtonText.text = redrawDefaultText;
            isGenerating = false;
            
        }
    }

    private void GenerateImageForCurrentStory()
    {
        StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));
    }

    IEnumerator FadeInContent()
    {
        contentCanvasGroup.alpha = 0f;

        float duration = 0.6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            contentCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        contentCanvasGroup.alpha = 1f;
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
        StartCoroutine(GenerateChapterImage(currentStory, currentChapterIndex));
    }

    public void OnDownloadDreamBookClicked()
    {
        if (!gameEnded)
        {
            Debug.Log("Dream book is only available after the ending.");
            return;
        }

        Debug.Log("Preparing to record your dream into a dream book...");
        StartCoroutine(ExportDreamBook());
    }

    private void ShowLoadingText()
    {
        loadingText.text = "The next chapter is forming in the astral realm...";
        loadingText.gameObject.SetActive(true);
        loadingText.transform.SetAsLastSibling();

        if (loadingPulseCoroutine != null)
        {
            StopCoroutine(loadingPulseCoroutine);
        }

        loadingPulseCoroutine = StartCoroutine(PulseLoadingText());
    }

    IEnumerator PulseLoadingText()
    {
        while (true)
        {
            float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);

            Color c = loadingText.color;
            c.a = alpha;
            loadingText.color = c;

            yield return null;
        }
    }

    private void HideLoadingText()
    {
        if (loadingPulseCoroutine != null)
        {
            StopCoroutine(loadingPulseCoroutine);
            loadingPulseCoroutine = null;
        }

        Color c = loadingText.color;
        c.a = 1f;
        loadingText.color = c;

        loadingText.gameObject.SetActive(false);
    }

    private void LogDreamBookHistory()
    {
        Debug.Log(
            $"Dream Book contains {chapterHistory.Count} chapters"
        );

        for (int i = 0; i < chapterHistory.Count; i++)
        {
            ChapterRecord chapter = chapterHistory[i];

            string imageStatus =
                string.IsNullOrEmpty(chapter.imagePath)
                    ? "missing image"
                    : "image saved";

            Debug.Log(
                $"{i + 1}. {chapter.chapterType} — {imageStatus}"
            );
        }
    }

    IEnumerator ExportDreamBook()
    {
        Debug.Log("ExportDreamBook coroutine entered!");

        if (chapterHistory == null || chapterHistory.Count == 0)
        {
            Debug.LogError("Cannot export dream book: no chapters saved.");
            yield break;
        }

        DreamBookExportRequest exportRequest =
            new DreamBookExportRequest
            {
                dream = currentDream,
                obstacles = currentObstacles,
                style = bookDreamStyle,
                created_at = bookCreatedAt,
                chapters = chapterHistory
            };

        Debug.Log("Sending dream book export request...");

        string exportJson =
            JsonUtility.ToJson(exportRequest);

        byte[] exportBytes =
            System.Text.Encoding.UTF8.GetBytes(exportJson);

        UnityWebRequest exportWebRequest =
            new UnityWebRequest(
                proxyUrl + "/export-pdf",
                "POST"
            );

        exportWebRequest.uploadHandler =
            new UploadHandlerRaw(exportBytes);

        exportWebRequest.downloadHandler =
            new DownloadHandlerBuffer();

        exportWebRequest.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        yield return exportWebRequest.SendWebRequest();

        if (
            exportWebRequest.result ==
            UnityWebRequest.Result.Success
        )
        {
            DreamBookExportResponse exportResponse =
                JsonUtility.FromJson<DreamBookExportResponse>(
                    exportWebRequest.downloadHandler.text
                );

            Debug.Log(
                "Dream book exported: " +
                exportResponse.pdf_path
            );

            Debug.Log(
                "Dream book URL: " +
                exportResponse.pdf_url
            );

            Application.OpenURL(exportResponse.pdf_url);
        }
        else
        {
            Debug.LogError(
                "Dream book export failed: " +
                exportWebRequest.error
            );

            Debug.LogError(
                "Export response: " +
                exportWebRequest.downloadHandler.text
            );
        }
    }

    [System.Serializable]
    public class DreamBookExportRequest
    {
        public string dream;
        public string obstacles;
        public string style;
        public string created_at;
        public List<ChapterRecord> chapters;
    }

    [System.Serializable]
    public class DreamBookExportResponse
    {
        public string pdf_path;
        public string pdf_url;
    }

    [System.Serializable]
    public class ChapterRecord
    {
        public string chapterType;
        public string chapterText;
        public string imagePath;
        public string chosenAction;
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