using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class HttpTest : MonoBehaviour
{
    [Header("Personajes")]
    // FIX: pon un fallback real para que no sea null si la request falla
    [SerializeField] private int[] characterIds = { 300, 2, 47 };

    [Header("URLs")]
    [SerializeField] private string APIUrl = "https://my-json-server.typicode.com/WatermelonSuggar/jsonDB-Wendy/users/";
    [SerializeField] private string RickAndMortyUrl = "https://rickandmortyapi.com/api/character/";

    [Header("UI - Imágenes")]
    [SerializeField] private RawImage[] rawImages;
    [SerializeField] private Texture2D placeholder;

    [Header("UI - Labels por carta")]
    [SerializeField] private TMP_Text[] titleLabels;
    [SerializeField] private TMP_Text[] subtitleLabels;

    [Header("UI - Encabezado")]
    [SerializeField] private TMP_Text playerNameLabel;
    [SerializeField] private TMP_Text MyNameLabel;

    [Header("Navegación")]
    private int currentUserId = 1;
    private int totalUsers = 4;

    [Header("UI - Siguiente")]
    [SerializeField] private Button rightArrowButton; // (puede quedar sin asignar)
    private bool isLoading = false;

    // DTOs
    [System.Serializable] public class ApiUser { public int id; public string username; public string name; public List<int> deck; }
    [System.Serializable] public class Character { public int id; public string name; public string status; public string species; public string image; }

    void OnValidate() {
        if (!string.IsNullOrEmpty(APIUrl) && !APIUrl.EndsWith("/")) APIUrl += "/";
        if (!string.IsNullOrEmpty(RickAndMortyUrl) && !RickAndMortyUrl.EndsWith("/")) RickAndMortyUrl += "/";
    }

    void Start() { ChangeUser(currentUserId); }

    // -------- Navegación --------
    public void ChangeUser(int newUserId)
    {
        if (newUserId < 1 || newUserId > totalUsers) return;
        if (isLoading) return;

        isLoading = true;
        if (rightArrowButton) rightArrowButton.interactable = false;

        currentUserId = newUserId;
        StopAllCoroutines();
        StartCoroutine(GetUser(currentUserId));
    }

    public void NextUser()
    {
        int next = currentUserId + 1;
        if (next > totalUsers) next = 1;
        ChangeUser(next);
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.RightArrow)) NextUser();
    }

    // -------- USER (JSON) --------
    IEnumerator GetUser(int userId)
    {
        var request = UnityWebRequest.Get(APIUrl + userId);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            Debug.LogError($"[USER] {request.responseCode} - {request.error}");
            LoadCharactersFromIds(characterIds); // usa fallback seguro
            isLoading = false;
            if (rightArrowButton) rightArrowButton.interactable = true;
            yield break;
        }

        // LOG: imprime el JSON crudo que vino del servidor
        string rawJson = request.downloadHandler.text;
        Debug.Log($"[USER JSON] {rawJson}");

        var user = JsonUtility.FromJson<ApiUser>(rawJson);
        string who = !string.IsNullOrEmpty(user.username) ? user.username : user.name;
        if (playerNameLabel) playerNameLabel.text = who;

        // LOG: imprime qué usuario estás mostrando y su deck
        string deckStr = (user.deck != null && user.deck.Count > 0) ? string.Join(",", user.deck) : "sin deck";
        Debug.Log($"[USER SHOWN] id={user.id}, username=\"{who}\", deck=[{deckStr}]");

        // usa deck si existe; si no, fallback
        if (user.deck != null && user.deck.Count > 0)
            LoadCharactersFromIds(user.deck);
        else
            LoadCharactersFromIds(characterIds);

        isLoading = false;
        if (rightArrowButton) rightArrowButton.interactable = true;
    }

    // -------- Pinta hasta 3 cartas --------
    void LoadCharactersFromIds(IList<int> ids)
    {
        int n = ids.Count;
        if (rawImages != null)      n = Mathf.Min(n, rawImages.Length);
        if (titleLabels != null)    n = Mathf.Min(n, titleLabels.Length);
        if (subtitleLabels != null) n = Mathf.Min(n, subtitleLabels.Length);

        for (int i = 0; i < n; i++)
            StartCoroutine(GetCharacterIntoSlot(ids[i], i));

        for (int j = n; j < rawImages.Length; j++) {
            if (rawImages[j]) rawImages[j].texture = placeholder ? placeholder : null;
            if (titleLabels != null && j < titleLabels.Length && titleLabels[j]) titleLabels[j].text = "";
            if (subtitleLabels != null && j < subtitleLabels.Length && subtitleLabels[j]) subtitleLabels[j].text = "";
        }
    }

    // -------- Rick & Morty --------
    IEnumerator GetCharacterIntoSlot(int characterId, int slotIndex)
    {
        if (rawImages == null || slotIndex < 0 || slotIndex >= rawImages.Length || rawImages[slotIndex] == null) {
            Debug.LogWarning($"Slot inválido: {slotIndex}");
            yield break;
        }

        var request = UnityWebRequest.Get(RickAndMortyUrl + characterId);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            Debug.LogError($"[CHAR] {request.responseCode} - {request.error}");
            yield break;
        }

        Character character = JsonUtility.FromJson<Character>(request.downloadHandler.text);

        if (titleLabels != null && slotIndex < titleLabels.Length && titleLabels[slotIndex] != null)
            titleLabels[slotIndex].text = character.name;

        if (subtitleLabels != null && slotIndex < subtitleLabels.Length && subtitleLabels[slotIndex] != null)
            subtitleLabels[slotIndex].text = $"{character.status} - {character.species}";

        yield return StartCoroutine(GetImageInto(character.image, rawImages[slotIndex]));
    }

    IEnumerator GetImageInto(string imageUrl, RawImage target)
    {
        if (target == null) yield break;

        var request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success) {
            Debug.LogError($"[IMG] {request.responseCode} - {request.error}");
            if (placeholder) target.texture = placeholder;
            yield break;
        }

        target.texture = DownloadHandlerTexture.GetContent(request);
    }

    // -------- Utilidad --------
    private void ClearUI()
    {
        if (rawImages != null)
            for (int i = 0; i < rawImages.Length; i++)
                if (rawImages[i]) rawImages[i].texture = placeholder ? placeholder : null;

        if (titleLabels != null)
            for (int i = 0; i < titleLabels.Length; i++)
                if (titleLabels[i]) titleLabels[i].text = "";

        if (subtitleLabels != null)
            for (int i = 0; i < subtitleLabels.Length; i++)
                if (subtitleLabels[i]) subtitleLabels[i].text = "";
    }
}
