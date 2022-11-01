using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour
{
    public Terrain terrain;
    public float perlinScalar;
    public int octaves;
    public float maxValue;

    public bool regen;
    public bool artificial;

    public float heightModifier = 0.1f;

    Trackable<float> _perlinTracker;
    Trackable<int> _octaveTracker;
    Trackable<float> _maxTracker;

    private void Awake()
    {
        CreateTrackers();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.Log("Editor application playing");
            if (regen)
            {
                if (!artificial)
                    CreateTerrain();
                else
                    CreateBowlTerrain();
                Debug.Log("Recreated terrain");
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (EditorApplication.isPlaying || !regen)
        {
            return;
        }
        if (ExistNullTrackers())
        {
            CreateTrackers();
        }
        if (_perlinTracker.Dirty() || _octaveTracker.Dirty() || _maxTracker.Dirty())
        {
            if (!artificial)
                CreateTerrain();
            else
                CreateBowlTerrain();
        }
    }

    bool ExistNullTrackers()
    {
        return _perlinTracker == null || _octaveTracker == null || _maxTracker == null;
    }

    void CreateTrackers()
    {
        _perlinTracker = new Trackable<float>(perlinScalar);
        _octaveTracker = new Trackable<int>(octaves);
        _maxTracker = new Trackable<float>(maxValue);
    }

    private void CreateTerrain()
    {
        int res = terrain.terrainData.heightmapResolution;
        float[,] newValues = new float[res, res];
        float scalar = perlinScalar;
        float value = maxValue;
        for (int o = 0; o < octaves; o++)
        {
            for (int i = 0; i < res; i++)
            {
                for (int j = 0; j < res; j++)
                {
                    float fi = i;
                    float fj = j;
                    newValues[i, j] = newValues[i, j] + Mathf.PerlinNoise(fi / res * scalar, fj / res * scalar) * value;
                }
            }
            value /= 2;
            scalar *= 2;
        }
        terrain.terrainData.SetHeights(0, 0, newValues);
    }

    private void CreateBowlTerrain()
    {
        int res = terrain.terrainData.heightmapResolution;
        float[,] newValues = new float[res, res];

        float cx = 17f;
        float cy = 17f;
        for (int i = 0; i < res; i++)
        {
            for (int j = 0; j < res; j++)
            {
                float fi = i;
                float fj = j;
                float dx = cx - fi;
                float dy = cy - fj;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                newValues[i, j] = dist * heightModifier;
            }
        }
        terrain.terrainData.SetHeights(0, 0, newValues);
    }
}
