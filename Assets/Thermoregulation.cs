//Thermoregulation mod for KTANE
//author: cameronos
//date: 11/7/2025
//music to make LOVE to /watch?v=5c4T3-5ybrY

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using Math = ExMath;

public class Thermoregulation : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;

    //Thermo stuff
    public KMSelectable[] buttons; // 3 buttons, assign in inspector
    public MeshRenderer fanBlades;
    public TextMesh lcdDisplay;
    public MeshRenderer statusLED;
    public Material[] ledMaterials;

    public float rotationSpeed = 200f;
    private bool fanOn = false;
    private bool buttonsAllowed = false;

    private float timeToOverheat; // Remove 'static'
    private float degreesPerSecond; // Remove initialization, calculate in Start()
    private float currentTemp = 75f; // Starting temperature
    private const float EXPLOSION_TEMP = 250f;

    // Solution tracking
    private List<int> correctSequence = new List<int>();
    private List<int> playerSequence = new List<int>();
    private int sequenceNumber;
    private int ledColor;

    static int ModuleIdCounter = 1;
    int ModuleId;
    private bool ModuleSolved;
    private bool bombExploded = false;

    void Awake()
    {
        ModuleId = ModuleIdCounter++;
        GetComponent<KMBombModule>().OnActivate += Activate;
        /*
      foreach (KMSelectable object in keypad) {
          object.OnInteract += delegate () { keypadPress(object); return false; };
      }
      */

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].OnInteract += delegate()
            {
                buttonPress(index);
                return false;
            };
        }
    }

    void buttonPress(int buttonIndex)
    {
        if (!buttonsAllowed || ModuleSolved)
        {
            return;
        }

        Audio.PlaySoundAtTransform("buttonPress", buttons[0].transform);
        buttons[buttonIndex].AddInteractionPunch(0.5f);

        if (buttonIndex < 3) // Colored buttons (0=Red, 1=Yellow, 2=Blue)
        {
            playerSequence.Add(buttonIndex); // Button index IS the sequence value!

            string currentInput = "";
            for (int i = 0; i < playerSequence.Count; i++)
            {
                if (i > 0)
                    currentInput += ", ";
                currentInput += GetColorName(playerSequence[i]);
            }

            Debug.LogFormat(
                "[Thermoregulation #{0}] Pressed {1} button. Current input: {2}",
                ModuleId,
                GetColorName(buttonIndex),
                currentInput
            );

            // Check if wrong button pressed
            int currentIndex = playerSequence.Count - 1;
            if (
                currentIndex >= correctSequence.Count
                || playerSequence[currentIndex] != correctSequence[currentIndex]
            )
            {
                Debug.LogFormat(
                    "[Thermoregulation #{0}] Wrong button! Expected {1}, got {2}. Strike!",
                    ModuleId,
                    GetColorName(correctSequence[currentIndex]),
                    GetColorName(buttonIndex)
                );
                Strike();
                playerSequence.Clear();
            }
        }
        else if (buttonIndex == 3) // Fan button
        {
            if (playerSequence.Count == 0)
            {
                Debug.LogFormat(
                    "[Thermoregulation #{0}] Fan pressed without sequence! Starting strike sequence.",
                    ModuleId
                );
                StartCoroutine(fanStrikeSequence());
            }
            else if (
                playerSequence.Count == correctSequence.Count
                && playerSequence.SequenceEqual(correctSequence)
            )
            {
                Debug.LogFormat(
                    "[Thermoregulation #{0}] Correct sequence entered! Module solved.",
                    ModuleId
                );
                ModuleSolved = true;
                rotationSpeed = 500f;
                Solve();
            }
            else
            {
                Debug.LogFormat(
                    "[Thermoregulation #{0}] Incomplete sequence ({1}/3 buttons). Strike!",
                    ModuleId,
                    playerSequence.Count
                );
                Strike();
                playerSequence.Clear();
            }
        }
    }

    void OnDestroy()
    { //Shit you need to do when the bomb ends
        bombExploded = true;
    }

    void Activate()
    { //Shit that should happen when the bomb arrives (factory)/Lights turn on
        StartCoroutine(fanBreakSequence());
    }

    void Start() //soon as bomb loads?
    {

      float bombTime = Bomb.GetTime(); // total bomb time in seconds

          if (bombTime <= 540f) //9min
          {
              timeToOverheat = 99999; // never rise infinite
              degreesPerSecond = 0f; // no automatic temp rise
              Debug.LogFormat(
                  "[Thermoregulation #{0}] Bomb has ≤9 minutes. Temp will not rise.",
                  ModuleId
              );
          }
          else
          {
              timeToOverheat = bombTime * 0.5f;
              degreesPerSecond = 175f / timeToOverheat;
              float timeToOverheatReadable = timeToOverheat/60;
              Debug.LogFormat(
                  "[Thermoregulation #{0}] Bomb has {1} minutes before overheating at 250°F. Temp will rise {2}°F per second.",
                  ModuleId,
                  timeToOverheatReadable,
                  degreesPerSecond
              );
          }

        //set LED
        ledColor = Rnd.Range(0, 3);
        if (statusLED != null && ledMaterials != null && ledMaterials.Length >= 3)
        {
            statusLED.material = ledMaterials[ledColor];
        }

        calculateSolution();
    }

    void calculateSolution()
    {
        int batteries = Bomb.GetBatteryCount();
        int indicators = Bomb.GetIndicators().Count();
        int modules = Bomb.GetSolvableModuleNames().Count;
        int ports = Bomb.GetPortCount();

        // Get sum of digits in serial number
        string serial = Bomb.GetSerialNumber();
        int serialDigitSum = 0;
        foreach (char c in serial)
        {
            if (char.IsDigit(c))
                serialDigitSum += (c - '0');
        }
        sequenceNumber = ((batteries * indicators) + (modules * ports) + serialDigitSum + ledColor) % 10;
        correctSequence.Add(sequenceNumber % 3);
        correctSequence.Add((sequenceNumber / 3) % 3);
        correctSequence.Add((sequenceNumber / 9) % 3);
        string ledColorName = ledColor == 0 ? "Green" : ledColor == 1 ? "Amber" : "Purple";
        Debug.LogFormat(
            "[Thermoregulation #{0}] Batteries: {1}, Indicators: {2}, Modules: {3}, Ports: {4}, Serial Digits: {5}, LED Color: {6} ({7})",
            ModuleId,
            batteries,
            indicators,
            modules,
            ports,
            serialDigitSum,
            ledColorName,
            ledColor
        );
        Debug.LogFormat("[Thermoregulation #{0}] Sequence number: {1}", ModuleId, sequenceNumber);
        Debug.LogFormat(
            "[Thermoregulation #{0}] Correct sequence: {1}",
            ModuleId,
            string.Join(" → ", correctSequence.Select(x => GetColorName(x)).ToArray())
        );
    }

    private IEnumerator fanBreakSequence() //at start of module
    {
        Audio.PlaySoundAtTransform("fanBreak", buttons[0].transform);
        yield return new WaitForSeconds(0.5f);
        for (float t = 0; t < 1.5f; t += Time.deltaTime)
        {
            rotationSpeed = Mathf.Lerp(0f, 1500f, t / 1.5f);
            yield return null;
        }
        rotationSpeed = 1500f;
        yield return new WaitForSeconds(5.3f - 0.5f - 1.5f);
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            rotationSpeed = Mathf.Lerp(1500f, 0f, t / 1.2f);
            yield return null;
        }
        rotationSpeed = 0f;
        buttonsAllowed = true;
    }

    private IEnumerator fanStrikeSequence() //in case of premature press
    {
        buttonsAllowed = false;
        Audio.PlaySoundAtTransform("fanBreak", buttons[0].transform);
        yield return new WaitForSeconds(0.5f);
        for (float t = 0; t < 1.5f; t += Time.deltaTime)
        {
            rotationSpeed = Mathf.Lerp(0f, 1500f, t / 1.5f);
            yield return null;
        }
        rotationSpeed = 2000f;
        yield return new WaitForSeconds(5.3f - 0.5f - 1.5f);
        for (float t = 0; t < 1.2f; t += Time.deltaTime)
        {
            rotationSpeed = Mathf.Lerp(1500f, 0f, t / 1.2f);
            yield return null;
        }
        Strike();
        rotationSpeed = 0f;
        buttonsAllowed = true;
    }

    void Update()
    {
        // Rotate fan blades
        if (fanBlades != null && rotationSpeed != 0f)
            fanBlades.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        if (buttonsAllowed && !bombExploded)
        {
            if (ModuleSolved)
            {
                if (currentTemp > 75f)
                {
                    currentTemp = Mathf.Max(
                        75f,
                        currentTemp - (degreesPerSecond * 2f * Time.deltaTime)
                    );
                }
            }
            else
            {
                if (rotationSpeed == 0f)
                {
                    currentTemp += degreesPerSecond * Time.deltaTime;

                    if (currentTemp >= EXPLOSION_TEMP && !bombExploded)
                    {
                        Debug.LogFormat(
                            "[Thermoregulation #{0}] Temperature reached {1}°F - KABOOM!",
                            ModuleId,
                            Mathf.RoundToInt(currentTemp)
                        );
                        bombExploded = true;
                        HandleStrikesFor250();
                        currentTemp = EXPLOSION_TEMP; //caps this to not do it again
                    }
                }
                else //fan is cooling
                {
                    currentTemp = Mathf.Max(
                        75f,
                        currentTemp - (degreesPerSecond * 0.5f * Time.deltaTime)
                    );
                }
            }

            // Update LCD display
            if (lcdDisplay != null)
                lcdDisplay.text = Mathf.RoundToInt(currentTemp) + "°F";
        }
    }

    void HandleStrikesFor250()
    {
        for (int i = 0; i < 99; i++)
        {
            Strike();
            // a disgusting way to do it, and I'm sure the community will have some bold opinion
            // but hey, YOU make it better. give ideas.
        }
    }

    string GetButtonName(int index)
    {
        switch (index)
        {
            case 0:
                return "Red";
            case 1:
                return "Yellow";
            case 2:
                return "Blue";
            case 3:
                return "Fan";
            default:
                return "Unknown";
        }
    }

    string GetColorName(int value)
    {
        switch (value)
        {
            case 0:
                return "Red";
            case 1:
                return "Yellow";
            case 2:
                return "Blue";
            default:
                return "Unknown";
        }
    }

    void Solve()
    {
        ModuleSolved = true;
        Audio.PlaySoundAtTransform("solveSound", buttons[0].transform);
        GetComponent<KMBombModule>().HandlePass();
    }

    void Strike()
    {
        GetComponent<KMBombModule>().HandleStrike();
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage =
        @"!{0} press <sequence> [Press buttons in order: r=Red, y=Yellow, b=Blue, f=Fan].";
    #pragma warning restore 414


        IEnumerator ProcessTwitchCommand(string Command)
        {
            string[] parts = Command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) yield break;
            if (parts[0] != "press") yield break;
            if (parts.Length < 2) yield break;

            List<KMSelectable> buttonsToPress = new List<KMSelectable>();

            for (int i = 1; i < parts.Length; i++)
            {
                switch (parts[i])
                {
                    case "r": buttonsToPress.Add(buttons[0]); break; // Red
                    case "y": buttonsToPress.Add(buttons[1]); break; // Yellow
                    case "b": buttonsToPress.Add(buttons[2]); break; // Blue
                    case "f": buttonsToPress.Add(buttons[3]); break; // Fan
                    default: yield break; // invalid input
                }
            }

            yield return null;

            foreach (KMSelectable button in buttonsToPress)
            {
                button.OnInteract();
                yield return new WaitForSeconds(0.3f);
            }
        }

        IEnumerator TwitchHandleForcedSolve()
        {
            while (!ModuleSolved)
            {
                playerSequence.Clear();

                foreach (int colorValue in correctSequence)
                {
                    int buttonIndex = colorValue; // 0=Red,1=Yellow,2=Blue
                    buttons[buttonIndex].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }

                buttons[3].OnInteract(); // Press fan
                yield return new WaitForSeconds(0.1f);
            }
        }

}
