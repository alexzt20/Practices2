using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ProceduralLevelGenerator : MonoBehaviour
{
   

    [Header("Cuadricula (Minimo 10x8)")]
    [Min(10)] public int width = 12;
    [Min(8)] public int height = 8;
    [Min(1f)] public float cellSize = 2f;

    [Header("Generacion")]
    [Range(0f, 0.45f)] public float wallProbability = 0.22f;
    [Min(0)] public int rewardCount = 5;
    [Min(0)] public int robotCount = 3;
    public int seed = 12345;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject startPrefab;
    public GameObject goalPrefab;      // Núcleo Central
    public GameObject rewardPrefab;
    public GameObject robotPrefab;     // Robots enemigos

    private int[,] map; // 0 = transitable, 1 = muro
    private readonly List<GameObject> generatedObjects = new List<GameObject>();

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        if (!ValidateConfiguration())
            return;

        ClearGenerated();

        // Control determinista de la semilla aleatoria
        Random.State previousState = Random.state;
        Random.InitState(seed);

        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int goal = new Vector2Int(width - 2, height - 2);

        map = new int[width, height];

        // PASO 1: APLICAR REGLAS DE CONSTRUCCIÓN Y RESTRICCIONES
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1; // Regla 1
                bool protectedCell = new Vector2Int(x, y) == start || new Vector2Int(x, y) == goal; // Restricción 1

                if (border)
                {
                    map[x, y] = 1;
                }
                else if (protectedCell)
                {
                    map[x, y] = 0;
                }
                else
                {
                    map[x, y] = Random.value < wallProbability ? 1 : 0; // Regla 2
                }
            }
        }

        // PASO 2: ESTRATEGIA DE CONECTIVIDAD (Regla 3)
        CarveGuaranteedPath(start, goal);

        // PASO 3: REPRESENTACIÓN VISUAL
        BuildGeometry();

        Spawn(startPrefab, CellToWorld(start, 0.5f), "Start");
        Spawn(goalPrefab, CellToWorld(goal, 0.5f), "Goal_NucleoCentral");

        // PASO 4: CONTENIDO ADICIONAL (Restricciones 2 y 3)
        List<Vector2Int> rewardPositions = SpawnRewards(start, goal);
        int spawnedRobots = SpawnRobots(start, goal, rewardPositions);

        // PASO 5: MISIÓN DINÁMICA BASADA EN PARÁMETROS
        string mission = $"Infiltración: Alcanza el Núcleo Central, recolecta {rewardPositions.Count} datos y evade {spawnedRobots} robots.";

        Debug.Log($"<b>[RESULTADO DE EVALUACIÓN]</b>\n" +
                  $"• Semilla: <b>{seed}</b> | Tamaño: <b>{width}x{height}</b>\n" +
                  $"• Probabilidad Muros: <b>{wallProbability * 100}%</b>\n" +
                  $"• Recompensas: <b>{rewardPositions.Count}</b> | Robots: <b>{spawnedRobots}</b>\n" +
                  $"• Misión: <i>{mission}</i>");

        Random.state = previousState;
    }

    private void CarveGuaranteedPath(Vector2Int start, Vector2Int goal)
    {
        for (int x = start.x; x <= goal.x; x++) map[x, start.y] = 0;
        for (int y = start.y; y <= goal.y; y++) map[goal.x, y] = 0;
    }

    private void BuildGeometry()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Spawn(floorPrefab, CellToWorld(cell, 0f), $"Floor_{x}_{y}");

                if (map[x, y] == 1)
                {
                    Spawn(wallPrefab, CellToWorld(cell, 0.5f), $"Wall_{x}_{y}");
                }
            }
        }
    }

    private List<Vector2Int> SpawnRewards(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> candidates = GetTransitableCells(start, goal);
        ShuffleList(candidates);

        int amount = Mathf.Min(rewardCount, candidates.Count);
        List<Vector2Int> spawnedPositions = new List<Vector2Int>();

        for (int i = 0; i < amount; i++)
        {
            Spawn(rewardPrefab, CellToWorld(candidates[i], 0.5f), $"Reward_{i}");
            spawnedPositions.Add(candidates[i]);
        }

        return spawnedPositions;
    }

    private int SpawnRobots(Vector2Int start, Vector2Int goal, List<Vector2Int> rewardPositions)
    {
        if (robotCount <= 0 || robotPrefab == null) return 0;

        List<Vector2Int> candidates = GetTransitableCells(start, goal);
        candidates.RemoveAll(cell => rewardPositions.Contains(cell)); // Restricción 3

        ShuffleList(candidates);

        int amount = Mathf.Min(robotCount, candidates.Count);

        for (int i = 0; i < amount; i++)
        {
            Spawn(robotPrefab, CellToWorld(candidates[i], 0.5f), $"Robot_{i}");
        }

        return amount;
    }

    private List<Vector2Int> GetTransitableCells(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (map[x, y] == 0 && cell != start && cell != goal)
                {
                    list.Add(cell);
                }
            }
        }
        return list;
    }

    private void ShuffleList(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private Vector3 CellToWorld(Vector2Int cell, float yPosition)
    {
        return transform.position + new Vector3(cell.x * cellSize, yPosition, cell.y * cellSize);
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, string objectName)
    {
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
        instance.name = objectName;
        generatedObjects.Add(instance);
        return instance;
    }

    private void ClearGenerated()
    {
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
        }
        generatedObjects.Clear();

        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        foreach (GameObject child in children)
        {
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private bool ValidateConfiguration()
    {
        if (width < 10 || height < 8)
        {
            Debug.LogError("Criterio Incumplido: La dimensión mínima debe ser 10 x 8 celdas.");
            return false;
        }
        if (floorPrefab == null || wallPrefab == null || startPrefab == null || goalPrefab == null)
        {
            Debug.LogError("Faltan Prefabs obligatorios en el Inspector.");
            return false;
        }
        return true;
    }
}