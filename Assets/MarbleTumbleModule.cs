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
public class MarbleTumbleModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public GameObject[] Cylinders;
    public Mesh[] Meshes;
    public Texture[] Textures;
    public KMSelectable Selectable;
    public GameObject MarbleLayer1; // layer used for rotation to match the rotation of the cylinder it’s in
    public GameObject MarbleLayer2; // layer used to “roll” the marble
    public GameObject MarbleLayer3; // layer used to rotate the marble about its center randomly

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    // Red, yellow, green, blue, silver
    private static Color[] _colors = "FF8181,EFF09A,81D682,8EB5FF,FFFFFF".Split(',').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private static int _numNotches = 10;
    private static int[] _notchMins = new[] { 3, 2, 1, 1, 1 };
    private static int[] _notchMaxs = new[] { 7, 8, 9, 9, 9 };
    private static int[][] _rotationData = @"-1,1,-2,0,2;-2,1,2,-1,0;1,0,2,-2,-1;0,-1,-2,1,2;2,0,1,-1,-2;1,-2,-1,2,0;-2,2,0,1,-1;0,-1,1,2,-2;-1,2,0,-2,1;2,-2,-1,0,1".Split(';').Select(str => str.Split(',').Select(s => int.Parse(s)).ToArray()).ToArray();

    private int[] _traps;
    private int[] _colorIxs;
    private int[] _rotations;
    private int _marbleDist;

    private sealed class RotationInfo
    {
        public int CylinderIndex;
        public int RotateFrom;
        public int RotateTo;
    }
    private abstract class Anim
    {
        public abstract IEnumerable<object> RunAnimation(MarbleTumbleModule m);
    }
    private sealed class RotationAnim : Anim
    {
        public RotationInfo[] Rotations;

        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            var elapsed = 0f;
            var duration = .1f;
            var fromAngles = Rotations.Select(inf => (360 / _numNotches) * inf.RotateFrom).ToArray();
            var toAngles = Rotations.Select(inf => (360 / _numNotches) * inf.RotateTo).ToArray();
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < 5; i++)
                    m.Cylinders[i].transform.localEulerAngles = new Vector3(0, easeOutSine(Mathf.Min(duration, elapsed), duration, fromAngles[i], toAngles[i]), 0);
                yield return null;
            }
        }
    }
    private sealed class MarbleIntoGap : Anim
    {
        public int IntoIndex;

        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            yield break;
        }
    }
    private sealed class MarbleIntoTrap : Anim
    {
        public int IntoIndex;

        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            yield break;
        }
    }

    private Queue<Anim> _queue = new Queue<Anim>();
    private Coroutine _rotateCoroutine;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _colorIxs = Enumerable.Range(0, 5).ToArray().Shuffle();
        _rotations = new int[5];
        _traps = new int[5];
        _marbleDist = 6;

        enqueueRotations(() =>
        {
            var textureIxs = Enumerable.Range(0, 5).ToArray().Shuffle();
            for (int i = 0; i < 5; i++)
            {
                _rotations[i] = Rnd.Range(i == 4 ? 1 : 0, _numNotches);
                do
                    _traps[i] = Rnd.Range(_notchMins[i], _notchMaxs[i] + 1);
                while (i == 4 && trap(4) == 0);

                var lookingFor = string.Format("Cylinder_{0}_{1}", i + 1, _traps[i]);
                Cylinders[i].GetComponent<MeshFilter>().mesh = Meshes.First(m => m.name.Equals(lookingFor));
                var mat = Cylinders[i].GetComponent<MeshRenderer>().material;
                mat.color = _colors[_colorIxs[i]];
                mat.mainTexture = Textures[textureIxs[i]];
            }
        });
        MarbleLayer3.transform.localEulerAngles = new Vector3(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360));

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
        _queue.Enqueue(new RotationAnim { Rotations = infs });
        if (_marbleDist > 0)
        {
            var marblePos = _marbleDist == 6 ? 0 : gap(_marbleDist);
            if (gap(_marbleDist - 1) == marblePos)
                _queue.Enqueue(new MarbleIntoGap { IntoIndex = _marbleDist - 1 });
            else if (trap(_marbleDist - 1) == marblePos)
                _queue.Enqueue(new MarbleIntoTrap { IntoIndex = _marbleDist - 1 });
        }
    }

    private int gap(int ix)
    {
        return (_rotations[ix] % _numNotches + _numNotches) % _numNotches;
    }

    private int trap(int ix)
    {
        return ((_rotations[ix] + _traps[ix]) % _numNotches + _numNotches) % _numNotches;
    }

    private static float easeOutSine(float time, float duration, float from, float to)
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
            foreach (var obj in infs.RunAnimation(this))
                yield return obj;
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
