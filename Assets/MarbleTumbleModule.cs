using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MarbleTumble;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Marble Tumble
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
    public GameObject MarbleLayer1; // layer used to match the rotation of the cylinder it’s in (rot Y)
    public GameObject MarbleLayer2; // layer used to move (pos X) and “roll” (rot -Z) the marble
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
    private Queue<Anim> _queue = new Queue<Anim>();

    private sealed class RotationInfo
    {
        public int CylinderIndex { get; private set; }
        public int RotateFrom { get; private set; }
        public int RotateTo { get; private set; }
        public RotationInfo(int cylinderIx, int rotFrom, int rotTo) { CylinderIndex = cylinderIx; RotateFrom = rotFrom; RotateTo = rotTo; }
    }
    private abstract class Anim
    {
        public abstract IEnumerable<object> RunAnimation(MarbleTumbleModule m);
        protected IEnumerable<object> animate(int cylinder, int marble, int from, int to, MarbleTumbleModule m, float delay = 0)
        {
            while (delay > 0)
            {
                yield return null;
                delay -= Time.deltaTime;
            }

            const float duration = .1f;
            var elapsed = 0f;
            var fromAngle = (360 / _numNotches) * from;
            var toAngle = (360 / _numNotches) * to;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var ang = Quaternion.Euler(0, easeOutSine(Mathf.Min(duration, elapsed), duration, fromAngle, toAngle), 0);
                m.Cylinders[cylinder].transform.localRotation = ang;
                if (cylinder == marble)
                    m.MarbleLayer1.transform.localRotation = ang;
                yield return null;
            }
        }
    }
    private sealed class RotationAnim : Anim
    {
        public RotationInfo[] Rotations { get; private set; }
        public int Marble { get; private set; }
        public RotationAnim(RotationInfo[] rot, int marble) { Rotations = rot; Marble = marble; }

        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            var coroutines = Rotations.Select(rot => animate(rot.CylinderIndex, Marble, rot.RotateFrom, rot.RotateTo, m).GetEnumerator()).ToArray();
            var any = true;
            while (any)
            {
                yield return null;
                any = false;
                for (int i = 0; i < coroutines.Length; i++)
                    any |= coroutines[i].MoveNext();
            }
        }
    }
    private sealed class MarbleInto : Anim
    {
        public int FromIndex { get; private set; }
        public int IntoIndex { get; private set; }
        public bool IsGap { get; private set; }
        public RotationInfo Rotation4 { get; private set; }
        private MarbleInto(int fromIx, int intoIx, bool gap, RotationInfo rotation4) { FromIndex = fromIx; IntoIndex = intoIx; IsGap = gap; Rotation4 = rotation4; }
        public static MarbleInto Gap(int fromIx, int intoIx) { return new MarbleInto(fromIx, intoIx, true, null); }
        public static MarbleInto Trap(int fromIx, int intoIx, RotationInfo rotation4) { return new MarbleInto(fromIx, intoIx, false, rotation4); }

        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            m.Audio.PlaySoundAtTransform("Marble" + (2 * (FromIndex - IntoIndex)), m.transform);
            var elapsed = 0f;
            var duration = .2f * (FromIndex - IntoIndex);
            var origRoll = m.MarbleLayer2.transform.localEulerAngles.z;
            var fromX = m.MarbleLayer2.transform.localPosition.x;
            var toX = IsGap ? (IntoIndex == 0 ? .002f : -.01f * (IntoIndex + .5f) - .003f) : -.01f * (IntoIndex + 1) - .0004f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var movement = easeInQuad(Mathf.Min(duration, elapsed), duration, 0, toX - fromX);
                m.SetMarblePos(movement + fromX);
                m.MarbleLayer2.transform.localEulerAngles = new Vector3(0, 0, origRoll - movement / .003f / Mathf.PI * 180);
                yield return null;
            }
            if (IntoIndex == 0 && IsGap)
            {
                m.Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, m.transform);
                m.Module.HandlePass();
            }
            else if (!IsGap)
            {
                m.Module.HandleStrike();

                var coroutines = (Rotation4 != null ? new[] { animate(4, -1, Rotation4.RotateFrom, Rotation4.RotateTo, m, .8f), marbleReset(m) } : new[] { marbleReset(m) }).Select(e => e.GetEnumerator()).ToArray();
                var any = true;
                while (any)
                {
                    yield return null;
                    any = false;
                    for (int i = 0; i < coroutines.Length; i++)
                        any |= coroutines[i].MoveNext();
                }
            }
        }

        private IEnumerable<object> marbleReset(MarbleTumbleModule m)
        {
            var orig1rot = m.MarbleLayer1.transform.localRotation;
            var orig2x = m.MarbleLayer2.transform.localPosition.x;
            var orig2rot = m.MarbleLayer2.transform.localRotation;
            var orig3rot = m.MarbleLayer3.transform.localRotation;
            var newRandomRot = Quaternion.Euler(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360));

            float duration = 1f;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Min(duration, elapsed) / duration;

                m.MarbleLayer1.transform.localRotation = Quaternion.Slerp(orig1rot, Quaternion.identity, t);
                m.SetMarblePos(easeOutSine(t, 1, orig2x, -.062f), t * (1 - t) * .1f);
                m.MarbleLayer2.transform.localRotation = Quaternion.Slerp(orig2rot, Quaternion.identity, t);
                m.MarbleLayer3.transform.localRotation = Quaternion.Slerp(orig3rot, newRandomRot, t);
                yield return null;
            }

            duration = .1f;
            elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                m.SetMarblePos(easeInQuad(Mathf.Min(duration, elapsed), duration, -.062f, -.058f));
                yield return null;
            }
        }
    }
    private sealed class Exit : Anim
    {
        public override IEnumerable<object> RunAnimation(MarbleTumbleModule m)
        {
            m._queue = null;
            yield break;
        }
    }

    private void SetMarblePos(float pos, float height = 0)
    {
        MarbleLayer1.transform.localPosition = new Vector3(0, -.0216f + .012f * (pos / (-.058f) + 1) + height, 0);
        MarbleLayer2.transform.localPosition = new Vector3(pos, 0, 0);
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _colorIxs = Enumerable.Range(0, 5).ToArray().Shuffle();
        _rotations = new int[5];
        _traps = new int[5];
        _marbleDist = 5;

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

            Debug.LogFormat(@"[Marble Tumble #{0}] Colors: {1}", _moduleId, _colorIxs.JoinString(", "));
            Debug.LogFormat(@"[Marble Tumble #{0}] Traps: {1}", _moduleId, _traps.JoinString(", "));
        });
        MarbleLayer3.transform.localEulerAngles = new Vector3(Rnd.Range(0, 360), Rnd.Range(0, 360), Rnd.Range(0, 360));

        StartCoroutine(rotate());
        Bomb.OnBombExploded += delegate
        {
            if (_queue != null)
                _queue.Enqueue(new Exit());
        };
        Selectable.OnInteract += delegate { click(); return false; };
    }

    private bool? enqueueRotations(Action action)
    {
        var prevRotations = _rotations.ToArray();
        action();
        Debug.LogFormat(@"[Marble Tumble #{0}] Rotations: {1}", _moduleId, _rotations.JoinString(", "));
        _queue.Enqueue(new RotationAnim(prevRotations.Select((prev, ix) => new RotationInfo(ix, prev, _rotations[ix])).ToArray(), _marbleDist));
        if (_marbleDist > 0)
        {
            var marblePos = _marbleDist == 5 ? 0 : gap(_marbleDist);
            var orig = _marbleDist;
            while (_marbleDist > 0 && gap(_marbleDist - 1) == marblePos)
                _marbleDist--;

            if (_marbleDist > 0 && trap(_marbleDist - 1) == marblePos)
            {
                Debug.LogFormat(@"[Marble Tumble #{0}] Marble falls into trap at level {1}. Strike!", _moduleId, _marbleDist - 1);
                int rotation4 =
                    (gap(4) == 0 && trap(4) == 1) || (trap(4) == 0 && gap(4) == 1) ? 1 :
                    (gap(4) == 0 || trap(4) == 0) ? -1 : 0;
                _queue.Enqueue(MarbleInto.Trap(orig, _marbleDist - 1, rotation4 == 0 ? null : new RotationInfo(4, _rotations[4], _rotations[4] + rotation4)));
                _rotations[4] += rotation4;
                _marbleDist = 5;
                return false;
            }
            else if (_marbleDist != orig)
            {
                _queue.Enqueue(MarbleInto.Gap(orig, _marbleDist));
                Debug.LogFormat(@"[Marble Tumble #{0}] Marble falls into gap at level {1}.{2}", _moduleId, _marbleDist, _marbleDist == 0 ? " Module solved." : null);
                if (_marbleDist == 0)
                    return true;
            }
        }
        return null;
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
    private static float easeInQuad(float time, float duration, float from, float to)
    {
        time /= duration;
        return (to - from) * time * time + from;
    }

    private IEnumerator rotate()
    {
        while (_queue != null)
        {
            while (_queue.Count == 0)
                yield return null;

            var infs = _queue.Dequeue();
            foreach (var obj in infs.RunAnimation(this))
                yield return obj;
        }
    }

    private bool? click()
    {
        if (_queue == null)
            return null;
        return enqueueRotations(() =>
        {
            var sec = ((int) Bomb.GetTime()) % 10;
            Debug.LogFormat(@"[Marble Tumble #{0}] Clicked when last seconds digit was: {1}", _moduleId, sec);
            for (int i = 0; i < 5; i++)
                _rotations[i] += _rotationData[sec][_colorIxs[i]];
        });
    }

#pragma warning disable 414
    private string TwitchHelpMessage = @"Use “!{0} 2/5” or “!{0} press 2/5” to press the module when the last digit in the timer is either 2 or 5. Use “!{0} 2/5 2”/“!{0} press 2/5 2” to press it twice. (Any amount is fine, but will only be pressed until the timer changes.)";
#pragma warning restore 414

    private int[] tryParse(string str)
    {
        var pieces = str.Split('/');
        var ints = new int[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
            if (!int.TryParse(pieces[i], out ints[i]))
                return null;
        return ints;
    }

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        int amount = 1;
        int[] vals;

        if ((pieces.Length == 2 && pieces[0] == "press" && (vals = tryParse(pieces[1])) != null) || (pieces.Length == 1 && (vals = tryParse(pieces[0])) != null) ||
            (pieces.Length == 3 && pieces[0] == "press" && (vals = tryParse(pieces[1])) != null && int.TryParse(pieces[2], out amount)) || (pieces.Length == 2 && (vals = tryParse(pieces[0])) != null && int.TryParse(pieces[1], out amount)))
        {
            if (vals.Any(v => v < 0 || v > 9))
            {
                yield return "sendtochaterror Last seconds digit must be 0–9.";
                yield break;
            }
            if (amount < 0)
            {
                yield return "sendtochaterror Negative amounts? I’m not even going to dignify that with an error message.";
                yield break;
            }

            yield return null;
            if (amount < 1)
                yield break;
            yield return new WaitUntil(() => vals.Contains(((int) Bomb.GetTime()) % 10));
            var val = ((int) Bomb.GetTime()) % 10;
            if (!vals.Contains(val))    // should never happen, but just to be sure
                yield break;
            for (int i = 0; i < amount; i++)
            {
                if (((int) Bomb.GetTime()) % 10 != val)     // stop if the desired second has passed
                    yield break;

                var result = click();
                if (result == true)
                {
                    yield return "solve";
                    yield break;
                }
                else if (result == false)
                {
                    yield return "strike";
                    yield break;
                }

                yield return new WaitForSeconds(.1f);
            }
        }
    }

    void TwitchHandleForcedSolve()
    {
        if (_marbleDist == 0 || _queue == null)
            return; // already solved or bomb blew up

        var target = _marbleDist == 5 ? 0 : _rotations[_marbleDist];
        _queue.Enqueue(new RotationAnim(Enumerable.Range(0, _marbleDist).Select(i => new RotationInfo(i, _rotations[i], (_rotations[i] / 10) * 10 + target)).ToArray(), _marbleDist));
        _queue.Enqueue(MarbleInto.Gap(_marbleDist, 0));
    }
}
