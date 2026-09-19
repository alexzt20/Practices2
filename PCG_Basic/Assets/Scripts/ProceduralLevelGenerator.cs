using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ProceduralLevelGenerator : MonoBehaviour
{
    [Header("Cuadricula")]
    [Min(5)] public int width = 12;
    [Min(5)] public int height = 8;
    [Min(1f)] public float cellSize = 2f;

    [Header("Generacion")]
    [Range(0f, 0.45f)]
    public float wallProbability = 0.22f;
    [Min(0)]
    public int rewardCount = 5;
    public int seed = 12345;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject startPrefab;
    public GameObject goalPrefab;
    public GameObject rewardPrefab;

    // 0 = transitable, 1 = muro
    private int[,] map;

    // Referencias para limpiar la generación previa
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

        // Guardamos el estado global para no alterar otros sistemas aleatorios.
        Random.State previousState = Random.state;
        Random.InitState(seed);

        Vector2Int start = new Vector2Int(1, 1);
        Vector2Int goal = new Vector2Int(width - 2, height - 2);

        map = new int[width, height];

        // Generación inicial del mapa
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                bool protectedCell = new Vector2Int(x, y) == start || new Vector2Int(x, y) == goal;

                if (border)
                {
                    map[x, y] = 1; // Muro
                }
                else if (protectedCell)
                {
                    map[x, y] = 0; // Transitable
                }
                else
                {
                    map[x, y] = Random.value < wallProbability ? 1 : 0; // Muro o transitable
                }
            }
        }

        // Restricción de jugabilidad (corredor en L garantizado)
        CaveGuaranteedPath(start, goal);

        // Representación visual 
        BuildGeometry();

        Spawn(startPrefab, CellToWorld(start, 0.5f), "Start");
        Spawn(goalPrefab, CellToWorld(goal, 0.5f), "Goal");

        // Contenido adicional
        int spawnedRewards = SpawnRewards(start, goal);

        string mission = "Llega a la meta y recoge " + spawnedRewards + " recompensas.";
        Debug.Log("Semilla: " + seed + " | Recompensas: " + spawnedRewards + " | Misión: " + mission);

        Random.state = previousState; // Restauramos el estado global
    }

    private void CaveGuaranteedPath(Vector2Int start, Vector2Int goal)
    {
        for (int x = start.x; x <= goal.x; x++)
        {
            map[x, start.y] = 0; // Transitable
        }
        for (int y = start.y; y <= goal.y; y++)
        {
            map[goal.x, y] = 0; // Transitable
        }
    }

    private void BuildGeometry()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                // Instanciar Suelo
                Spawn(floorPrefab, CellToWorld(cell, 0f), "Floor_" + x + "_" + y);

                // Instanciar Muro (si corresponde)
                if (map[x, y] == 1)
                {
                    Spawn(wallPrefab, CellToWorld(cell, 0.5f), "Wall_" + x + "_" + y);
                }
            }
        }
    }

    private int SpawnRewards(Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (map[x, y] == 0 && cell != start && cell != goal)
                {
                    candidates.Add(cell);
                }
            }
        }

        // Mezcla Fisher-Yates corregida
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        int amount = Mathf.Min(rewardCount, candidates.Count);

        for (int i = 0; i < amount; i++)
        {
            Spawn(rewardPrefab, CellToWorld(candidates[i], 0.5f), "Reward_" + i);
        }

        return amount;
    }

    private Vector3 CellToWorld(Vector2Int cell, float yPosition)
    {
        return transform.position + new Vector3(cell.x * cellSize, yPosition, cell.y * cellSize);
    }

    // CORRECCIÓN: Método Spawn implementado
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

        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private bool ValidateConfiguration()
    {
        if (width < 5 || height < 5)
        {
            Debug.LogError("El mapa debe tener al menos 5 x 5 celdas.");
            return false;
        }
        if (floorPrefab == null || wallPrefab == null || startPrefab == null || goalPrefab == null)
        {
            Debug.LogError("Faltan prefabs obligatorios en el Inspector.");
            return false;
        }
        if (rewardCount > 0 && rewardPrefab == null)
        {
            Debug.LogError("RewardPrefab debe estar asignado si rewardCount es mayor a 0.");
            return false;
        }

        return true;
    }
}











