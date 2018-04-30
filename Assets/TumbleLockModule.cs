using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TumbleLock;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Tumble Lock
/// Created by Timwi
/// </summary>
public class TumbleLockModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public GameObject[] Cylinders;
    public Mesh[] Meshes;
    public Texture[] Textures;
    public KMSelectable Selectable;
    public GameObject Marble;
    public GameObject MarbleContainer;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    // Red, yellow, green, blue, silver
    private static Color[] _colors = "FF8181,EFF09A,81D682,8EB5FF,FFFFFF".Split(',').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private static int[] _numNotches = new[] { 4, 5, 6, 8, 10 };
    private static int[][] _rotationData = @"-1,1,-2,0,2;-2,1,2,-1,0;1,0,2,-2,-1;0,-1,-2,1,2;2,0,1,-1,-2;1,-2,-1,2,0;-2,2,0,1,-1;0,-1,1,2,-2;-1,2,0,-2,1;2,-2,-1,0,1".Split(';').Select(str => str.Split(',').Select(s => int.Parse(s)).ToArray()).ToArray();

    private int[] _traps;
    private int[] _colorIxs;
    private int[] _rotations;

    private sealed class RotationInfo
    {
        public int CylinderIndex;
        public int RotateFrom;
        public int RotateTo;
    }
    private Queue<RotationInfo[]> _queue = new Queue<RotationInfo[]>();
    private Coroutine _rotateCoroutine;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _colorIxs = Enumerable.Range(0, 5).ToArray().Shuffle();
        _rotations = new int[5];
        _traps = new int[5];

        enqueueRotations(() =>
        {
            var textureIxs = Enumerable.Range(0, 5).ToArray().Shuffle();
            for (int i = 0; i < 5; i++)
            {
                _rotations[i] = Rnd.Range(i == 4 ? 1 : 0, _numNotches[i]);
                do
                    _traps[i] = Rnd.Range(1, _numNotches[i]);
                while (i == 4 && ((_rotations[i] + _traps[i]) % _numNotches[i] + _numNotches[i]) % _numNotches[i] == 0);

                var lookingFor = string.Format("Cylinder_{0}_{1}", i + 1, _traps[i]);
                Cylinders[i].GetComponent<MeshFilter>().mesh = Meshes.First(m => m.name.Equals(lookingFor));
                var mat = Cylinders[i].GetComponent<MeshRenderer>().material;
                mat.color = _colors[_colorIxs[i]];
                mat.mainTexture = Textures[textureIxs[i]];
            }
        });
        Marble.transform.localEulerAngles = new Vector3(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360));

        _rotateCoroutine = StartCoroutine(rotate());
        Bomb.OnBombExploded += delegate { StopCoroutine(_rotateCoroutine); };
        Selectable.OnInteract += click;
    }

    private void enqueueRotations(Action action)
    {
        var infs = Enumerable.Range(0, 5).Select(i => new RotationInfo { CylinderIndex = i, RotateFrom = _rotations[i] }).ToArray();
        action();
        for (int i = 0; i < 5; i++)
            infs[i].RotateTo = _rotations[i];
        _queue.Enqueue(infs);
    }

    private float easeOutSine(float time, float duration, float from, float to)
    {
        return (to - from) * Mathf.Sin(time / duration * (Mathf.PI / 2)) + from;
    }

    private IEnumerator rotate()
    {
        while (true)
        {
            while (_queue.Count == 0)
                yield return null;

            var infs = _queue.Dequeue();
            var elapsed = 0f;
            var duration = .1f;
            var fromAngles = infs.Select(inf => (360 / _numNotches[inf.CylinderIndex]) * inf.RotateFrom).ToArray();
            var toAngles = infs.Select(inf => (360 / _numNotches[inf.CylinderIndex]) * inf.RotateTo).ToArray();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < 5; i++)
                    Cylinders[i].transform.localEulerAngles = new Vector3(0, easeOutSine(Mathf.Min(duration, elapsed), duration, fromAngles[i], toAngles[i]), 0);
                yield return null;
            }
        }
    }

    private bool click()
    {
        enqueueRotations(() =>
        {
            var sec = ((int) Bomb.GetTime()) % 10;
            Debug.LogFormat("[Tumble Lock #{0}] You clicked at {1}.", _moduleId, sec);
            for (int i = 0; i < 5; i++)
                _rotations[i] += _rotationData[sec][_colorIxs[i]];
        });
        return false;
    }
}
