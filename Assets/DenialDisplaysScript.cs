using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class DenialDisplaysScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable DenySel;
    public KMSelectable ConfirmSel;
    public KMSelectable[] ArrowSels;
    public GameObject[] CellObjects;
    public TextMesh[] DisplayTexts;
    public TextMesh[] DialTexts;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private static readonly Vector3[] _cellPositions = new Vector3[8]
    {
        new Vector3(-0.055f, 0.0125f, 0.055f),
        new Vector3(0f, 0.0125f, 0.055f),
        new Vector3(-0.055f, 0.0125f, 0f),
        new Vector3(0f, 0.0125f, 0f),
        new Vector3(0.055f, 0.0125f, 0f),
        new Vector3(-0.055f, 0.0125f, -0.055f),
        new Vector3(0f, 0.0125f, -0.055f),
        new Vector3(0.055f, 0.0125f, -0.055f)
    };
    private static readonly Func<int[], int>[][] _tableRules = new Func<int[], int>[10][]
    {
        new Func<int[], int>[3]
        {
            i => i[0] * i[4],
            i => i[3] * i[4] * 73,
            i => i[0] = i[2]
        },
        new Func<int[], int>[3]
        {
            i => i[4] * i[4],
            i => (i[2] + i[3]) / 2,
            i => i[1] * 7
        },
        new Func<int[], int>[3]
        {
            i => i[0] + i[2] + i[3],
            i => 50 * i[0],
            i => (i[4] + i[2]) * 3
        },
        new Func<int[], int>[3]
        {
            i => i[1] * i[1],
            i => i[2] * i[4],
            i => (i[3] + i[2])/2
        },
        new Func<int[], int>[3]
        {
            i => i[3] * 3,
            i => i[2] + (i[0] * i[1]),
            i => 548 * i[4]
        },
        new Func<int[], int>[3]
        {
            i => i[3] - i[1],
            i => i[0] * (i[2] + i[3]),
            i => 100 + i[2]
        },
        new Func<int[], int>[3]
        {
            i => i[2] * i[2] * i[2],
            i => i[4] / 2,
            i => i[1] - i[3] - i[0]
        },
        new Func<int[], int>[3]
        {
            i => i[1] * i[3],
            i => 43 * i[4],
            i => i[0] + i[2]
        },
        new Func<int[], int>[3]
        {
            i => i[3] - i[4],
            i => i[0] * i[0],
            i => (i[0] + i[4])/4
        },
        new Func<int[], int>[3]
        {
            i => i[2] * i[1],
            i => i[3] * 69,
            i => (i[4] - i[1]) * 4
        }
    };

    private int[] _dialNums = new int[3];
    private readonly int[] _displayNums = new int[5];
    private int _denialValue;

    private bool _expectingConfirm;
    private int _expectedInput;
    private readonly bool[] _changedScreens = new bool[5];
    private int[] _screensLeftToChange = new int[0];

    private readonly Coroutine[] _digitChangeAnimations = new Coroutine[5];

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        DenySel.OnInteract += DenyPress;
        ConfirmSel.OnInteract += ConfirmPress;
        for (int i = 0; i < ArrowSels.Length; i++)
            ArrowSels[i].OnInteract += ArrowPress(i);

        _dialNums = new int[3] { Rnd.Range(0, 10), Rnd.Range(0, 10), Rnd.Range(0, 10) };
        RandomizeCells();

        for (int i = 0; i < 5; i++)
        {
            int rnd = Rnd.Range(0, 3);
            if (rnd == 0)
                _displayNums[i] = Rnd.Range(0, 10);
            else if (rnd == 1)
                _displayNums[i] = Rnd.Range(10, 100);
            else
                _displayNums[i] = Rnd.Range(100, 1000);
        }
        Debug.LogFormat("[Denial Displays #{0}] Generated screens: {1}.", _moduleId, _displayNums.Join(", "));
        Calculate();
        for (int i = 0; i < 5; i++)
        {
            DisplayTexts[i].text = _displayNums[i].ToString();
            DisplayTexts[i].transform.localScale = _displayNums[i] > 99 ? new Vector3(0.001f, 0.001f, 100f) : _displayNums[i] > 9 ? new Vector3(0.00125f, 0.00125f, 100f) : new Vector3(0.00165f, 0.00165f, 100f);
        }
        for (int i = 0; i < 3; i++)
            DialTexts[i].text = _dialNums[i].ToString();
    }

    private bool DenyPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, DenySel.transform);
        DenySel.AddInteractionPunch(0.5f);
        if (_moduleSolved)
            return false;
        Debug.LogFormat("[Denial Displays #{0}] ===============================================", _moduleId);
        if (_expectingConfirm)
        {
            Module.HandleStrike();
            if (!_changedScreens.Contains(false))
                Debug.LogFormat("[Denial Displays #{0}] DENY was pressed when it had already been pressed five times. Strike.", _moduleId);
            else
                Debug.LogFormat("[Denial Displays #{0}] DENY was pressed when input was expected. Strike.", _moduleId);
        }
        else
        {
            Debug.LogFormat("[Denial Displays #{0}] DENY correctly pressed.", _moduleId);
        }
        _screensLeftToChange = Enumerable.Range(0, 5).Where(i => !_changedScreens[i]).ToArray();
        if (_screensLeftToChange.Length != 0)
        {
            int rand = _screensLeftToChange.PickRandom();
            _changedScreens[rand] = true;
            int rnd = Rnd.Range(0, 3);
            int before = _displayNums[rand];
            if (rnd == 0)
                _displayNums[rand] = Rnd.Range(0, 10);
            else if (rnd == 1)
                _displayNums[rand] = Rnd.Range(10, 100);
            else
                _displayNums[rand] = Rnd.Range(100, 1000);
            _digitChangeAnimations[rand] = StartCoroutine(ChangeDigit(rand, before, _displayNums[rand]));
            Debug.LogFormat("[Denial Displays #{0}] Screen {1} has been changed to {2}.", _moduleId, "ABCDE"[rand], _displayNums[rand]);
            Debug.LogFormat("[Denial Displays #{0}] Generating next stage.", _moduleId);
            Calculate();
        }
        return false;
    }

    private bool ConfirmPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ConfirmSel.transform);
        ConfirmSel.AddInteractionPunch(0.5f);
        if (_moduleSolved)
            return false;
        Debug.LogFormat("[Denial Displays #{0}] ===============================================", _moduleId);
        if (!_expectingConfirm)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Denial Displays #{0}] CONFIRM was pressed when input was not expected. Strike.", _moduleId);
        }
        else
        {
            if (_dialNums[0] == _expectedInput / 100 && _dialNums[1] == _expectedInput % 100 / 10 && _dialNums[2] == _expectedInput % 10)
            {
                _moduleSolved = true;
                Module.HandlePass();
                Audio.PlaySoundAtTransform("Solve", transform);
                Debug.LogFormat("[Denial Displays #{0}] The dials were set to the correct numbers. Module solved.", _moduleId);
            }
            else
            {
                Module.HandleStrike();
                Debug.LogFormat("[Denial Displays #{0}] The dials were set to {1}, when {2} was expected. Strike.", _moduleId, _dialNums.Join(""), _expectedInput.ToString("000"));
            }
        }
        return false;
    }

    private KMSelectable.OnInteractHandler ArrowPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ArrowSels[btn].transform);
            ArrowSels[btn].AddInteractionPunch(0.2f);
            if (_moduleSolved)
                return false;
            if (btn / 3 == 0)
                _dialNums[btn % 3] = (_dialNums[btn % 3] + 1) % 10;
            else
                _dialNums[btn % 3] = (_dialNums[btn % 3] + 9) % 10;
            for (int i = 0; i < 3; i++)
                DialTexts[i].text = _dialNums[i].ToString();
            return false;
        };
    }


    private void RandomizeCells()
    {
        var firstArr = Enumerable.Range(0, 8).ToArray().Shuffle().Take(3).ToArray();
        for (int i = 0; i < 3; i++)
            CellObjects[i + 5].transform.localPosition = _cellPositions[firstArr[i]];
        var secondArr = Enumerable.Range(0, 8).Where(i => !firstArr.Contains(i)).ToArray();
        for (int i = 0; i < 5; i++)
            CellObjects[i].transform.localPosition = _cellPositions[secondArr[i]];
    }

    private void Calculate()
    {
        Debug.LogFormat("[Denial Displays #{0}] ===============================================", _moduleId);
        _expectingConfirm = false;

        _denialValue = 0;
        // If every digit appears exactly once...
        if (_displayNums.Join("").Distinct().Count() == 10 && _displayNums.Join("").Count() == 10)
        {
            _denialValue += 3;
            Debug.LogFormat("[Denial Displays #{0}] Every digit appears exactly once. Adding 3.", _moduleId);
        }

        // If every display has 3 digits...
        if (_displayNums.All(i => i >= 100))
        {
            _denialValue += 2;
            Debug.LogFormat("[Denial Displays #{0}] Every display is 3 digits long. Adding 2.", _moduleId);
        }

        // If all 5 display add up to 1500 or more...
        if (_displayNums.Sum() >= 1500)
        {
            _denialValue += 2;
            Debug.LogFormat("[Denial Displays #{0}] All 5 displays add up to 1500 or more. Adding 2.", _moduleId);
        }

        // If there are less than 8 digits in total...
        if (_displayNums.Join("").Count() >= 5 && _displayNums.Join("").Count() < 8)
        {
            _denialValue += 2;
            Debug.LogFormat("[Denial Displays #{0}] There are less than 8 digits in total. Adding 2.", _moduleId);
        }

        // If there are 3 or more two-digit displays...
        if (_displayNums.Where(i => i >= 10 && i <= 99).Count() >= 3)
        {
            _denialValue++;
            Debug.LogFormat("[Denial Displays #{0}] There are 3 or more 2-digit displays. Adding 1.", _moduleId);
        }

        // If there is an even number of odd digits...
        if (_displayNums.Join("").Where(i => (i - '0') % 2 == 1).Count() % 2 == 0)
        {
            _denialValue++;
            Debug.LogFormat("[Denial Displays #{0}] There is an even number of odd digits. Adding 1.", _moduleId);
        }

        // For each displays in the 600s...
        int sixhundredCount = _displayNums.Where(i => i >= 600 && i <= 699).Count();
        _denialValue += sixhundredCount;
        if (sixhundredCount != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} displays in the 600s. Adding {1}.", _moduleId, sixhundredCount);

        // For each 3-digit displays with digits in ascending order...
        int ascending = _displayNums.Where(i => i >= 100 && i.ToString()[0] < i.ToString()[1] && i.ToString()[1] < i.ToString()[2]).Count();
        _denialValue += ascending;
        if (ascending != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} three digit displays with digits in ascending order. Adding {1}.", _moduleId, ascending);

        // If there are more even than odd digits...
        if (_displayNums.Join("").Where(i => (i - '0') % 2 == 0).Count() > _displayNums.Join("").Where(i => (i - '0') % 2 == 1).Count())
        {
            _denialValue++;
            Debug.LogFormat("[Denial Displays #{0}] There are more even digits than odd digits. Adding 1.", _moduleId);
        }

        // For each 2-digit display that is a multiple of 3...
        int multsOfThree = _displayNums.Where(i => i >= 10 && i <= 99 && i % 3 == 0).Count();
        _denialValue += multsOfThree;
        if (multsOfThree != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} 2-digit displays that are multiples of 3. Adding {1}.", _moduleId, multsOfThree);

        // If a digit appears on all 5 displays...
        if (Enumerable.Range(0, 10).Any(i => _displayNums.Select(j => j.ToString().Select(k => k - '0')).All(j => j.Contains(i))))
        {
            _denialValue -= 3;
            Debug.LogFormat("[Denial Displays #{0}] A digit appears on all 5 displays. Subtracting 3.", _moduleId);
        }

        // If every display has 1 digit...
        if (_displayNums.All(i => i <= 9))
        {
            _denialValue -= 2;
            Debug.LogFormat("[Denial Displays #{0}] Every display is 1 digit long. Subtracting 2.", _moduleId);
        }

        // If all 5 displays add up to 250 or less...
        if (_displayNums.Sum() <= 250)
        {
            _denialValue -= 2;
            Debug.LogFormat("[Denial Displays #{0}] All 5 displays add up to 250 or less. Subtracting 2.", _moduleId);
        }

        // If there are greater than 11 digits in total...
        if (_displayNums.Join("").Count() >= 11 && _displayNums.Join("").Count() > 11)
        {
            _denialValue -= 2;
            Debug.LogFormat("[Denial Displays #{0}] There are greater than 11 digits in total. Subtracting 2.", _moduleId);
        }

        // If there are 3 or more 1-digit displays...
        if (_displayNums.Where(i => i <= 9).Count() >= 3)
        {
            _denialValue--;
            Debug.LogFormat("[Denial Displays #{0}] There are 2 or more 1-digit displays. Subtracting 1.", _moduleId);
        }

        // If there is an odd number of even digits...
        if (_displayNums.Join("").Where(i => (i - '0') % 2 == 0).Count() % 2 == 1)
        {
            _denialValue--;
            Debug.LogFormat("[Denial Displays #{0}] There is an odd number of even digits. Subtracting 1.", _moduleId);
        }

        // For each display in the 200s...
        int twohundreds = _displayNums.Where(i => i >= 200 && i <= 299).Count();
        _denialValue -= twohundreds;
        if (twohundreds != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} displays in the 200s. Subtracting {1}.", _moduleId, twohundreds);

        // For each 3-digit display with digits in descending order...
        int descending = _displayNums.Where(i => i >= 100 && i.ToString()[0] > i.ToString()[1] && i.ToString()[1] > i.ToString()[2]).Count();
        _denialValue -= descending;
        if (descending != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} 3-digit displays with digits in descending order. Subtracting {1}.", _moduleId, descending);

        // If there are more odd than even digits...
        if (_displayNums.Join("").Where(i => (i - '0') % 2 == 1).Count() > _displayNums.Join("").Where(i => (i - '0') % 2 == 0).Count())
        {
            _denialValue--;
            Debug.LogFormat("[Denial Displays #{0}] There are more odd digits than even digits Subtracting 1.", _moduleId);
        }

        // For each 3-digit display that is a multiple of 5...
        int multsOfFive = _displayNums.Where(i => i >= 100 && i <= 999 && i % 5 == 0).Count();
        _denialValue -= multsOfFive;
        if (multsOfFive != 0)
            Debug.LogFormat("[Denial Displays #{0}] There are {1} 3-digit displays that are multiples of 5. Subtracting {1}.", _moduleId, multsOfFive);

        Debug.LogFormat("[Denial Displays #{0}] Final value for this stage: {1}", _moduleId, _denialValue);
        if (_denialValue <= 0)
        {
            Debug.LogFormat("[Denial Displays #{0}] The final value is negative or zero.", _moduleId);
            if (_changedScreens.Contains(false))
                Debug.LogFormat("[Denial Displays #{0}] Press DENY.", _moduleId);
            else
            {
                _expectingConfirm = true;
                int val = _displayNums.Sum() - Math.Abs(_denialValue);
                while (val > 1000)
                    val -= 100;
                _expectedInput = val;
                Debug.LogFormat("[Denial Displays #{0}] DENY has been pressed at least five times. Expected input: {1}", _moduleId, _expectedInput.ToString("000"));
            }
        }
        else
        {
            _expectingConfirm = true;
            int section = (_denialValue + _displayNums.Join("").Count()) % 10;
            int subsection = _displayNums[0].ToString().Length - 1;
            _expectedInput = Math.Abs(_tableRules[section][subsection](_displayNums)) % 1000;
            Debug.LogFormat("[Denial Displays #{0}] The final value is positive.", _moduleId);
            Debug.LogFormat("[Denial Displays #{0}] Use section {1} and subsection {2}.", _moduleId, section, subsection + 1);
            Debug.LogFormat("[Denial Displays #{0}] Expected input: {1}", _moduleId, _expectedInput.ToString("000"));
        }
    }

    private IEnumerator ChangeDigit(int display, int before, int after)
    {
        Audio.PlaySoundAtTransform("Deny" + Rnd.Range(1, 4).ToString(), transform);
        var beforeScale = before > 99 ? new Vector3(0.001f, 0.001f, 100f) : before > 9 ? new Vector3(0.00125f, 0.00125f, 100f) : new Vector3(0.00165f, 0.00165f, 100f);
        var afterScale = after > 99 ? new Vector3(0.001f, 0.001f, 100f) : after > 9 ? new Vector3(0.00125f, 0.00125f, 100f) : new Vector3(0.00165f, 0.00165f, 100f);
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            DisplayTexts[display].transform.localScale = new Vector3(Mathf.Lerp(beforeScale.x, 0, elapsed / duration), Mathf.Lerp(beforeScale.y, 0, elapsed / duration), 100f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        DisplayTexts[display].text = after.ToString();
        elapsed = 0f;
        while (elapsed < duration)
        {
            DisplayTexts[display].transform.localScale = new Vector3(Mathf.Lerp(0, afterScale.x, elapsed / duration), Mathf.Lerp(0, afterScale.y, elapsed / duration), 100f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        DisplayTexts[display].transform.localScale = new Vector3(afterScale.x, afterScale.y, 100f);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} deny [Press the deny button.] | !{0} submit 123 [Enter 123 into the dials, then press the CONFIRM button.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*deny\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            DenySel.OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*(?:submit)\s+(?<d>(\d){3})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            var num = m.Groups["d"].Value;
            for (int i = 0; i < 3; i++)
            {
                while (_dialNums[i] != num[i] - '0')
                {
                    ArrowSels[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            ConfirmSel.OnInteract();
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!_moduleSolved)
        {
            if (!_expectingConfirm)
            {
                DenySel.OnInteract();
                yield return new WaitForSeconds(0.4f);
                continue;
            }
            for (int i = 0; i < 3; i++)
            {
                while (_dialNums[i] != _expectedInput.ToString("000")[i] - '0')
                {
                    ArrowSels[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            ConfirmSel.OnInteract();
        }
    }
}
