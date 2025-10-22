using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TMPro;
using UnityEngine.EventSystems;

public class GameBoard : MonoBehaviour
{
    [Header("Patterns")]
    [SerializeField] private Pattern[] availablePatterns;
    [SerializeField] private TMP_Dropdown patternDropdown;

    [Header("UI")]
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private UnityEngine.UI.Button playButton;
    [SerializeField] private UnityEngine.UI.Button pauseButton;
    [SerializeField] private UnityEngine.UI.Slider speedSlider;

    [Header("Tilemaps & Tiles")]
    [SerializeField] private Tilemap currentState;
    [SerializeField] private Tilemap nextState;
    [SerializeField] private Tile aliveTile;
    [SerializeField] private Tile deadTile;
    
    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.05f;
    [SerializeField] private float minUpdateInterval = 0.05f;
    [SerializeField] private float maxUpdateInterval = 1f;

    private bool isPaused = true;
    private Tile currentBrushTile;
    private readonly HashSet<Vector3Int> aliveCells = new();
    private readonly HashSet<Vector3Int> cellsToCheck = new();

    public int population { get; private set; }
    public int iterations { get; private set; }
    public float time { get; private set; }

    private void Start()
    {
        InitializePatternDropdown();
        currentBrushTile = aliveTile;
        
        if (speedSlider != null)
        {
            speedSlider.minValue = minUpdateInterval;
            speedSlider.maxValue = maxUpdateInterval;
            speedSlider.value = updateInterval;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
        
        UpdateUI();
    }

    private void InitializePatternDropdown()
    {
        if (patternDropdown != null && availablePatterns != null && availablePatterns.Length > 0)
        {
            patternDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (Pattern p in availablePatterns)
            {
                options.Add(p.name);
            }
            patternDropdown.AddOptions(options);
            patternDropdown.onValueChanged.AddListener(OnPatternSelected);
        }
    }

    public void OnPatternSelected(int index)
    {
        if (index >= 0 && index < availablePatterns.Length)
        {
            SetPattern(availablePatterns[index]);
        }
    }

    private void Update()
    {
        // Обработка кликов мыши в режиме паузы
        if (isPaused && Input.GetMouseButton(0))
        {
            // Проверяем что не кликаем по UI элементам
            if (!IsPointerOverUI())
            {
                HandleMouseClick();
            }
        }
        
        UpdateStatistics();
    }

    // Проверка что курсор не над UI элементом
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleMouseClick()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int cell = currentState.WorldToCell(worldPos);
        
        // Убедимся что координаты валидные
        if (Mathf.Abs(cell.x) > 1000 || Mathf.Abs(cell.y) > 1000)
            return;

        if (currentBrushTile == aliveTile)
        {
            // Ставим живую клетку
            if (!aliveCells.Contains(cell))
            {
                currentState.SetTile(cell, aliveTile);
                aliveCells.Add(cell);
                population = aliveCells.Count;
                Debug.Log($"Set alive at {cell}");
            }
        }
        else
        {
            // Убираем клетку
            if (aliveCells.Contains(cell))
            {
                currentState.SetTile(cell, null);
                aliveCells.Remove(cell);
                population = aliveCells.Count;
                Debug.Log($"Set dead at {cell}");
            }
        }
        
        UpdateStatistics();
    }

    // Методы для кнопок смены кисти
    public void SetBrushAlive()
    {
        currentBrushTile = aliveTile;
        Debug.Log("Brush set to ALIVE");
    }

    public void SetBrushDead()
    {
        currentBrushTile = deadTile;
        Debug.Log("Brush set to DEAD");
    }

    private void UpdateStatistics()
    {
        if (infoText != null)
        {
            infoText.text = $"Population: {population}\n" +
                           $"Iterations: {iterations}\n" +
                           $"Time: {time:F1}s\n" +
                           $"State: {(isPaused ? "PAUSED" : "RUNNING")}";
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        UpdateUI();
    }

    public void Play()
    {
        isPaused = false;
        UpdateUI();
    }

    public void Pause()
    {
        isPaused = true;
        UpdateUI();
    }

    public void OnSpeedChanged(float value)
    {
        updateInterval = value;
        StopAllCoroutines();
        StartCoroutine(Simulate());
    }

    public void RandomizeBoard()
    {
        Clear();
        
        int size = 50;
        for (int x = -size/2; x < size/2; x++)
        {
            for (int y = -size/2; y < size/2; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (Random.Range(0, 100) < 20)
                {
                    currentState.SetTile(cell, aliveTile);
                    aliveCells.Add(cell);
                }
            }
        }
        
        population = aliveCells.Count;
        UpdateUI();
    }

    public void ClearBoard()
    {
        Clear();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (playButton != null) playButton.gameObject.SetActive(isPaused);
        if (pauseButton != null) pauseButton.gameObject.SetActive(!isPaused);
    }

    private IEnumerator Simulate()
    {
        var interval = new WaitForSeconds(updateInterval);
        yield return interval;

        while (enabled)
        {
            if (!isPaused)
            {
                UpdateState();
                population = aliveCells.Count;
                iterations++;
                time += updateInterval;
            }
            
            yield return interval;
        }
    }

    public void SetPattern(Pattern newPattern)
    {
        Debug.Log($"SetPattern called with: {(newPattern != null ? newPattern.name : "NULL")}");
    
        Clear();

        if (newPattern != null)
        {
            string patternName = newPattern.name.ToLower();
            Debug.Log($"Pattern name: {patternName}");
        
            if (patternName.Contains("random"))
            {
                Debug.Log("Detected RANDOM pattern");
                RandomizeBoard();
            }
            else if (patternName.Contains("empty"))
            {
                Debug.Log("Detected EMPTY pattern - clearing board");
                currentState.ClearAllTiles();
                aliveCells.Clear();
                population = 0;
                UpdateStatistics();
            }
            else
            {
                Debug.Log("Detected REGULAR pattern");
                Vector2Int center = newPattern.GetCenter();
                Debug.Log($"Pattern center: {center}, cells count: {newPattern.cells.Length}");

                for (int i = 0; i < newPattern.cells.Length; i++)
                {
                    Vector3Int cell = (Vector3Int)(newPattern.cells[i] - center);
                    currentState.SetTile(cell, aliveTile);
                    aliveCells.Add(cell);
                }
                population = aliveCells.Count;
            }
        }
        UpdateUI();
    }

    private void Clear()
    {
        aliveCells.Clear();
        cellsToCheck.Clear();
        currentState.ClearAllTiles();
        nextState.ClearAllTiles();
        population = 0;
        iterations = 0;
        time = 0f;
    }

    private void OnEnable()
    {
        StartCoroutine(Simulate());
    }

    private void UpdateState()
    {
        cellsToCheck.Clear();

        foreach (Vector3Int cell in aliveCells)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    cellsToCheck.Add(cell + new Vector3Int(x, y));
                }
            }
        }

        foreach (Vector3Int cell in cellsToCheck)
        {
            int neighbors = CountNeighbors(cell);
            bool alive = aliveCells.Contains(cell);

            if (!alive && neighbors == 3)
            {
                nextState.SetTile(cell, aliveTile);
                aliveCells.Add(cell);
            }
            else if (alive && (neighbors < 2 || neighbors > 3))
            {
                nextState.SetTile(cell, deadTile);
                aliveCells.Remove(cell);
            }
            else
            {
                nextState.SetTile(cell, currentState.GetTile(cell));
            }
        }

        Tilemap temp = currentState;
        currentState = nextState;
        nextState = temp;
        nextState.ClearAllTiles();
    }

    private int CountNeighbors(Vector3Int cell)
    {
        int count = 0;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                
                Vector3Int neighbor = cell + new Vector3Int(x, y);
                if (aliveCells.Contains(neighbor))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsAlive(Vector3Int cell)
    {
        return currentState.GetTile(cell) == aliveTile;
    }
}