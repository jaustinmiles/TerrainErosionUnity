using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

enum Direction: uint
{
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    UpLeft = 1 << 4,
    UpRight = 1 << 5,
    DownLeft = 1 << 6,
    DownRight = 1 << 7,
    None = 1 << 8
}

[ExecuteInEditMode]
public class Drainage : MonoBehaviour
{
    [FormerlySerializedAs("t")] public Terrain terrain;

    private int _res;
    private float[,] _elevn;
    private Direction[,] _ddirn;
    private int[,] _dlabl;
    private int[,] _dbasn;
    private int[,] _darea;
    private int[,] _dlnks;
    private Dictionary<int, Vector2> _basinMinima;

    private readonly List<Vector2> _directions = new List<Vector2>()
    {
        Vector2.down,
        Vector2.left,
        Vector2.right,
        Vector2.up,
        Vector2.down + Vector2.left,
        Vector2.down + Vector2.right,
        Vector2.up + Vector2.left,
        Vector2.up + Vector2.right
    };

    private readonly Dictionary<Vector2, Direction> _directionMapping = new Dictionary<Vector2, Direction>()
    {
        { Vector2.down, Direction.Down },
        { Vector2.left, Direction.Left },
        { Vector2.right, Direction.Right },
        { Vector2.up, Direction.Up },
        { Vector2.down + Vector2.left, Direction.DownLeft },
        { Vector2.down + Vector2.right, Direction.DownRight },
        { Vector2.up + Vector2.left, Direction.UpLeft },
        { Vector2.up + Vector2.right, Direction.UpRight },
    };

    private readonly Dictionary<Direction, Vector2> _inverseDirectionMapping = new Dictionary<Direction, Vector2>()
    {
        { Direction.Down, Vector2.down },
        { Direction.Left, Vector2.left },
        { Direction.Right, Vector2.right },
        { Direction.Up, Vector2.up },
        { Direction.DownLeft, Vector2.down + Vector2.left },
        { Direction.DownRight, Vector2.down + Vector2.right },
        { Direction.UpLeft, Vector2.up + Vector2.left },
        { Direction.UpRight, Vector2.up + Vector2.right },
        { Direction.None , Vector2.zero}
    };


    [FormerlySerializedAs("ShowDDIRN")] public bool showDdirn;
    [FormerlySerializedAs("ShowDLABL")] public bool showDlabl;
    [FormerlySerializedAs("ShowDBASN")] public bool showDbasn;
    [FormerlySerializedAs("ShowBasinMinima")] public bool showBasinMinima;
    [FormerlySerializedAs("ShowRidges")] public bool showRidges;
    [FormerlySerializedAs("ShowRidgeMinima")] public bool showRidgeMinima;

    private bool _queueUpdate = true;

    public float sigma;


    private Dictionary<int, Color> _dbasnColors;

    public Drainage(int[,] darea, int[,] dlnks)
    {
        _darea = darea;
        _dlnks = dlnks;
    }

    private void Reset()
    {
        Awake();
    }

    // Start is called before the first frame update
    void Awake()
    {
        _res = terrain.terrainData.heightmapResolution;
        _queueUpdate = true;
    }

    void DoUpdate()
    {
        _elevn = terrain.terrainData.GetHeights(0, 0, _res, _res);
        GetDdirn();
        GetDlabl();
        GetDbasn();
        GetPits();
        TearDown();
        GetDbasn();
        Debug.Log(_basinMinima.Count);
        if (_basinMinima.Count <= 1)
        {
            foreach (var item in _basinMinima)
            {
                CarvePath(item.Value, new Vector2(-1, -1));
                terrain.terrainData.SetHeights(0, 0, _elevn);
            }
            _queueUpdate = false;
        }
    }

    void GetDdirn()
    {
        _ddirn = new Direction[_res, _res];
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                Vector2 baseV = new Vector2(i, j);
                float baseElevation = ElevationAtVec(baseV);
                
                Direction minDir = Direction.None;
                float maxDelta = Single.NegativeInfinity;
                foreach (var v in _directions)
                {
                    var vOffset = v;
                    var newV = baseV + vOffset;
                    if (!CheckBounds(newV)) continue;
                    float newElevation = ElevationAtVec(newV);

                    if (newElevation < baseElevation)
                    {
                        // TODO: need to do the distance divided by sqrt(2) for diagonals
                        float delta = baseElevation - newElevation;
                        if (IsDiagonal(v)) delta /= Mathf.Sqrt(2);
                        if (delta > maxDelta)
                        {
                            minDir = _directionMapping[v];
                            maxDelta = delta;
                        }
                    }
                }

                _ddirn[i, j] = minDir;
            }
        }
    }

    Vector2 MinimumRidgePointInBasin(int basin)
    {
        Vector2 minPoint = Vector2.zero;
        float minHeight = Single.MaxValue;
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                if (_dbasn[i, j] == basin)
                {
                    if (IsEdge(i, j))
                    {
                        Vector2 p = new Vector2(i, j);
                        if (ElevationAtVec(p) < minHeight)
                        {
                            minPoint = p;
                            minHeight = ElevationAtVec(p);
                        }
                    }
                }
            }
        }

        return minPoint;
    }

    int MinimumNeighboringBasinAtEdge(Vector2 index)
    {
        int basin = At(_dbasn, index);
        float minHeight = Single.MaxValue;
        int neighbor = basin;
        foreach (Vector2 d in _directions)
        {
            Vector2 i2 = index + d;
            if (!CheckBounds(i2)) continue;
            if (At(_dbasn, i2) == basin)
                continue;
            if (ElevationAtVec(i2) < minHeight)
            {
                neighbor = At(_dbasn, i2);
                minHeight = ElevationAtVec(i2);
            }
        }

        return neighbor;
    }

    void TearDown()
    {
        HashSet<Tuple<int, int>> processed = new HashSet<Tuple<int, int>>(); 
        foreach (var item in _basinMinima)
        {
            int basin = item.Key;
            Vector2 minimum = item.Value;
            Vector2 lowRidge = MinimumRidgePointInBasin(basin);
            int neighborBasin = MinimumNeighboringBasinAtEdge(lowRidge);
            if (neighborBasin == basin) continue;
            if (processed.Contains(new Tuple<int, int>(basin, neighborBasin)) || processed.Contains(new Tuple<int, int>(neighborBasin, basin)))
                continue;
            Vector2 neighborMin = _basinMinima[neighborBasin];
            CarvePath(minimum, neighborMin);

            processed.Add(new Tuple<int, int>(basin, neighborBasin));
            processed.Add(new Tuple<int, int>(neighborBasin, basin));
        }
        terrain.terrainData.SetHeights(0, 0, _elevn);
    }

    void CarvePath(Vector2 start, Vector2 end)
    {
        float y1 = ElevationAtVec(start);
        float y2 = ElevationAtVec(end);
        Vector2 taller = y1 > y2 ? start : end;
        Vector2 shorter = taller == start ? end : start;

        float h1 = ElevationAtVec(taller);
        float h2 = ElevationAtVec(shorter);
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                Vector2 vec = new Vector2(i, j);
                float diff1 = h1 - ElevationAtVec(vec);
                float diff2 = h2 - ElevationAtVec(vec);
                float g = GaussianOnToLine(taller, shorter, diff1, diff2, vec);
                
                SetElevationAtVecRounded(new Vector2(i, j), ElevationAtVec(vec) + g);
            }
        }
    }

    private float GaussianScalar(float x, float sigmaValue)
    {
        double numerator = -1 * (x * x);
        double denominator = (2.0 * sigmaValue * sigmaValue);
        double expression = Math.Pow(Math.E, numerator / denominator);
        return (float)expression;
    }

    float GaussianOnToLine(Vector2 start, Vector2 end, float startX, float endX, Vector2 pos)
    {
        Vector2 diff = end - start;
        Vector2 vec = pos - start;
        Vector2 projected = Vector2.Dot(vec, diff.normalized) * diff.normalized;
        float dist;
        float val;
        if (projected.magnitude < diff.magnitude && Vector2.Dot(vec, diff) > 0)
        {
            dist = (vec - projected).magnitude;
            float t = (projected.magnitude / diff.magnitude);
            val =  t * endX  + (1f - t) * startX;
        }
        else
        {
            dist = Mathf.Min((pos - start).magnitude, (pos - end).magnitude);
            val = (pos - start).magnitude < (pos - end).magnitude ? startX : endX;
        }
        return GaussianScalar(dist, sigma) * val;
    }

    bool IsEdge(int i, int j)
    {
        Vector2 pos = new Vector2(i, j);
        int basin = At(_dbasn, pos);
        foreach (Vector2 d in _directions)
        {
            if (!CheckBounds(pos + d))
                continue;
            if (At(_dbasn, pos + d) != basin)
                return true;
        }

        return false;
    }

    T At<T>(T[,] arr, Vector2 index)
    {
        int x = (int)index.x;
        int y = (int)index.y;
        return arr[x, y];
    }

    void GetDlabl()
    {
        _dlabl = new int[_res, _res];
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                var baseV = new Vector2(i, j);
                if (_ddirn[i, j] != Direction.None) _dlabl[i, j]++;
                var dir = _inverseDirectionMapping[_ddirn[i, j]];
                var newV = baseV + dir;
                if (!CheckBounds(newV)) continue;
                _dlabl[(int)newV.x, (int)newV.y] += 100;
            }
        }
    }

    void GetDbasn()
    {
        _dbasnColors = new Dictionary<int, Color>();
        _dbasn = new int[_res, _res];
        UnionFind uf = new UnionFind(_res * _res);
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                var baseV = new Vector2(i, j);
                if (_ddirn[i, j] == Direction.None) continue;
                var dir = _inverseDirectionMapping[_ddirn[i, j]];
                var newV = baseV + dir;
                if (!CheckBounds(newV)) continue;
                uf.Union(i * _res + j, (int)(newV.x * _res + newV.y));
            }
        }

        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                _dbasn[i, j] = uf.At(i * _res + j);
            }
        }
    }

    void GetPits()
    {
        _basinMinima = new Dictionary<int, Vector2>();
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                int basin = _dbasn[i, j];
                if (!_basinMinima.ContainsKey(basin))
                {
                    _basinMinima[basin] = new Vector2(i, j);
                }
                else
                {
                    Vector2 prev = _basinMinima[basin];
                    if (GetWorldPointOnTerrain((int)prev.x, (int)prev.y).y >
                        GetWorldPointOnTerrain(i, j).y)
                    {
                        _basinMinima[basin] = new Vector2(i, j);
                    }
                }
            }
        }
    }

    void SetElevationAtVecRounded(Vector2 index, float value)
    {
        _elevn[Mathf.RoundToInt(index.y), Mathf.RoundToInt(index.x)] = value;
    }

    float ElevationAtVec(Vector2 index)
    {
        if (!CheckBounds(index)) return 0.2f;
        return _elevn[(int)index.y, (int)index.x];
    }

    bool CheckBounds( Vector2 input)
    {
        return CheckDim(input.x) && CheckDim(input.y);
    }

    bool CheckDim(float val)
    {
        return Mathf.Round(val) >= 0 && Mathf.Round(val) < _res;
    }

    // Update is called once per frame
    void Update()
    {
        if (_queueUpdate && EditorApplication.isPlaying)
        {
            DoUpdate();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DoUpdate();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_dbasn == null)
        {
            _elevn = terrain.terrainData.GetHeights(0, 0, _res, _res);
            GetDdirn();
            GetDlabl();
            GetDbasn();
            GetPits();
            GetDbasn();
        }
        if (showDdirn)
            DrawDdrinGizmos();
        if (showDlabl)
            DrawDlablGizmos();
        if (showDbasn)
            DrawDbasnGizmos();
        if (showBasinMinima)
            DrawBasinMinimaGizmos();
        if (showRidges)
            DrawRidges();
        if (showRidgeMinima)
            DrawRidgeMinimaGizmos();
    }

    private void DrawRidgeMinimaGizmos()
    {
        foreach (var item in _basinMinima)
        {
            Vector2 minima = MinimumRidgePointInBasin(item.Key);
            DrawSphereOnTerrainAtResPoint((int)minima.x, (int)minima.y, _dbasnColors[item.Key]);
        }
    }

    private void DrawRidges()
    {
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                if (IsEdge(i, j))
                {
                    DrawSphereOnTerrainAtResPoint(i, j, Color.red);
                }
            }
        }
    }

    private void DrawBasinMinimaGizmos()
    {
        foreach (var item in _basinMinima)
        {
            Vector2 vec = item.Value;
            DrawSphereOnTerrainAtResPoint((int)vec.x, (int)vec.y, _dbasnColors[item.Key]);
        }
    }

    private void DrawDbasnGizmos()
    {
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                int val = _dbasn[i, j];
                if (!_dbasnColors.ContainsKey(val))
                {
                    _dbasnColors[val] = new Color(Rand(), Rand(), Rand());
                }
                DrawSquareOnTerrainAtResPoint(i, j, _dbasnColors[val]);
            }
        }
    }

    private float Rand()
    {
        return Random.Range(0, 1f);
    }

    private void DrawDlablGizmos()
    {
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                var val = _dlabl[i, j];
                // pit (depression)
                if (val == 0)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.black);
                // ridge
                if (val == 1)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.magenta);
                // pit
                if (val == 100)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.blue);
                // Link
                if (val == 101)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.cyan);
                // Pit (hollow)
                if (val > 100 && val % 100 == 0)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.gray);
                // Fork
                if (val > 101 && val % 100 != 0)
                    DrawSphereOnTerrainAtResPoint(i, j, Color.red);
            }
        }
    }

    private void DrawSphereOnTerrainAtResPoint(int i, int j, Color color)
    {
        var worldPoint = GetWorldPointOnTerrain(i, j);
        Gizmos.color = color;
        Gizmos.DrawSphere(worldPoint, 0.25f);
    }
    
    private void DrawSquareOnTerrainAtResPoint(int i, int j, Color color)
    {
        var worldPoint = GetWorldPointOnTerrain(i, j);
        Gizmos.color = color;
        Gizmos.DrawCube(worldPoint, new Vector3(0.5f, 0.1f, 0.5f));
    }

    private Vector3 GetWorldPointOnTerrainVec(Vector2 index)
    {
        return GetWorldPointOnTerrain((int)index.x, (int)index.y);
    }

    private Vector3 GetWorldPointOnTerrain(int i, int j)
    {
        var terrainData = terrain.terrainData;
        var position = this.transform.position;
        float x = (float)i / _res * terrainData.size.x + position.x;
        float y = (float)j / _res * terrainData.size.z + position.z;
        float z = terrainData.GetHeight(i, j) + position.y;
        return new Vector3(x, z * 1.1f, y);
    }

    private void DrawDdrinGizmos()
    {
        for (int i = 0; i < _res; ++i)
        {
            for (int j = 0; j < _res; ++j)
            {
                if (_ddirn[i, j] == Direction.None) continue;
                Vector2 direction = _inverseDirectionMapping[_ddirn[i, j]];
                Vector3 d3 = new Vector3(direction.x, 0, direction.y);
                var worldPoint = GetWorldPointOnTerrain(i, j);   
                DrawArrow(worldPoint, d3, Color.blue);
            }
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 30.0f)
    {
        float thickness = 4;
        var terrainData = terrain.terrainData;
        float extent = terrainData.size.x / terrainData.heightmapResolution;
        Handles.color = color;
        direction *= extent * 0.6f;
        var p2 = pos + direction;
        Handles.DrawLine(pos, p2, thickness);
       
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);

        var p3 = p2 + right * arrowHeadLength;
        var p4 = p2 + left * arrowHeadLength;
        Handles.DrawLine(p2, p3, thickness);
        Handles.DrawLine(p2, p4, thickness);
    }

    private bool IsDiagonal(Vector2 v)
    {
        Direction d = _directionMapping[v];
        if (d == Direction.DownLeft || d == Direction.DownRight || d == Direction.UpLeft || d == Direction.UpRight)
            return true;
        return false;
    }
}
