using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

public static class WeightliftingSceneBuilder
{
    // Color Palette matching reference image
    static readonly Color BgDarkPanel   = new Color(0.086f, 0.098f, 0.125f, 0.94f); // #161920
    static readonly Color CardBorder    = new Color(0.20f, 0.23f, 0.28f, 0.65f);
    static readonly Color ChipBlue      = new Color(0.118f, 0.533f, 0.898f);        // #1E88E5
    static readonly Color ChipOrange    = new Color(0.984f, 0.549f, 0.000f);        // #FB8C00
    static readonly Color ChipGreen     = new Color(0.000f, 0.784f, 0.325f);        // #00C853
    static readonly Color GoldYellow    = new Color(1.000f, 0.839f, 0.000f);        // #FFD600
    static readonly Color StreakGold    = new Color(1.000f, 0.835f, 0.310f);        // #FFD54F
    static readonly Color NeonCyan      = new Color(0.000f, 0.690f, 1.000f);        // #00B0FF
    static readonly Color GreyText      = new Color(0.65f, 0.68f, 0.74f);
    static readonly Color TrackDark     = new Color(0.15f, 0.17f, 0.21f, 0.90f);

    static Sprite roundSprite, circleSprite;

    [MenuItem("Tools/Build Weightlifting Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Scenes");

        roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // ---------------- PBR Materials
        Material skinMat       = MakeMaterial("Skin", new Color(0.88f, 0.69f, 0.55f), false, 0.0f, 0.38f);
        Material hairMat       = MakeMaterial("Hair", new Color(0.13f, 0.11f, 0.11f), false, 0.05f, 0.40f);
        Material tankMat       = MakeMaterial("TankTop", new Color(0.12f, 0.18f, 0.28f), false, 0.0f, 0.45f); // Deep navy tank
        Material shortsMat     = MakeMaterial("Shorts", new Color(0.11f, 0.11f, 0.13f), false, 0.0f, 0.35f);
        Material stripeMat     = MakeMaterial("WhiteStripe", new Color(0.95f, 0.95f, 0.95f), false, 0.0f, 0.50f);
        Material hexIronMat    = MakeMaterial("HexIron", new Color(0.14f, 0.15f, 0.17f), false, 0.45f, 0.60f); // Matte dark hex dumbbell
        Material chromeMat     = MakeMaterial("Chrome", new Color(0.85f, 0.88f, 0.90f), false, 0.95f, 0.92f);
        Material floorMat      = MakeMaterial("GymFloor", new Color(0.10f, 0.11f, 0.13f), false, 0.15f, 0.55f);
        Material wallMat       = MakeMaterial("GymWall", new Color(0.14f, 0.15f, 0.18f), false, 0.05f, 0.25f);
        Material benchMat      = MakeMaterial("BenchFrame", new Color(0.16f, 0.17f, 0.20f), false, 0.5f, 0.6f);
        Material leatherMat    = MakeMaterial("BenchLeather", new Color(0.08f, 0.08f, 0.09f), false, 0.05f, 0.60f);
        Material towelMat      = MakeMaterial("Towel", new Color(0.82f, 0.84f, 0.88f), false, 0.0f, 0.20f);
        Material bottleMat     = MakeMaterial("Bottle", new Color(0.20f, 0.45f, 0.80f), false, 0.1f, 0.70f);
        Material plantMat      = MakeMaterial("Plant", new Color(0.12f, 0.35f, 0.18f), false, 0.0f, 0.40f);
        Material potMat        = MakeMaterial("PlantPot", new Color(0.13f, 0.14f, 0.16f), false, 0.2f, 0.50f);
        Material wallLogoMat   = MakeMaterial("WallLogo", new Color(0.38f, 0.42f, 0.50f), false, 0.4f, 0.60f);
        Material ghostMat      = MakeMaterial("GhostHex", new Color(0.15f, 1.0f, 0.35f, 0.45f), true, 0.1f, 0.90f, true, new Color(0.0f, 0.9f, 0.4f) * 2.5f);

        // ---------------- 3D Cinematic Lighting (Studio Gym Atmosphere)
        // Light: add or configure sun directional light
        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun == null)
        {
            sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }
        else
        {
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }

        // Spotlight over the lifter and wall logo
        GameObject spotGo = new GameObject("OverheadSpotlight");
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.transform.position = new Vector3(0.6f, 5.2f, 0.8f);
        spot.transform.rotation = Quaternion.Euler(72f, -18f, 0f);
        spot.spotAngle = 60f;
        spot.range = 10f;
        spot.intensity = 2.8f;
        spot.color = new Color(0.92f, 0.96f, 1.0f);
        spot.shadows = LightShadows.Soft;

        // Cool blue rim light illuminating the lifter's back and shoulders
        GameObject rimGo = new GameObject("RimLight");
        Light rim = rimGo.AddComponent<Light>();
        rim.type = LightType.Directional;
        rim.transform.rotation = Quaternion.Euler(20f, 135f, 0f);
        rim.color = new Color(0.20f, 0.55f, 1.0f);
        rim.intensity = 0.55f;
        rim.shadows = LightShadows.None;

        // ---------------- Camera setup (Perspective side view of the arm)
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.transform.position = new Vector3(6f, 2.3f, 0.6f);
        cam.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        // ---------------- 3D Gym Environment
        // Dark reflective gym floor
        Make("Floor", PrimitiveType.Plane, null, Vector3.zero, Vector3.zero, new Vector3(4f, 1f, 4f), floorMat);

        // Dark brick/textured gym back wall
        Make("BackWall", PrimitiveType.Cube, null, new Vector3(-3.2f, 4.5f, 0.6f), Vector3.zero, new Vector3(0.2f, 9f, 26f), wallMat);

        // Embossed "EMG LIFT" Wall Logo behind lifter (from reference photo)
        GameObject wallLogo = new GameObject("WallLogo_EMG_LIFT");
        wallLogo.transform.position = new Vector3(-3.08f, 3.75f, 1.35f);
        wallLogo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        // Barbell icon
        Make("LogoBar", PrimitiveType.Cube, wallLogo.transform, new Vector3(0f, 0.40f, 0f), Vector3.zero, new Vector3(0.40f, 0.05f, 0.02f), wallLogoMat);
        Make("LogoPlateL", PrimitiveType.Cube, wallLogo.transform, new Vector3(-0.22f, 0.40f, 0f), Vector3.zero, new Vector3(0.06f, 0.22f, 0.02f), wallLogoMat);
        Make("LogoPlateR", PrimitiveType.Cube, wallLogo.transform, new Vector3( 0.22f, 0.40f, 0f), Vector3.zero, new Vector3(0.06f, 0.22f, 0.02f), wallLogoMat);
        // "EMG LIFT" text block on wall
        Make("LogoTextPlate", PrimitiveType.Cube, wallLogo.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.95f, 0.18f, 0.02f), wallLogoMat);

        // Gym Flat Bench with Water Shaker Bottle and Folded Towel on the left
        GameObject benchGroup = new GameObject("BenchWithTowelAndBottle");
        benchGroup.transform.position = new Vector3(-1.4f, 0f, -1.8f);
        benchGroup.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        // Bench legs & pad
        Make("BenchLegL", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.25f, -0.65f), Vector3.zero, new Vector3(0.08f, 0.50f, 0.08f), benchMat);
        Make("BenchLegR", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.25f,  0.65f), Vector3.zero, new Vector3(0.08f, 0.50f, 0.08f), benchMat);
        Make("BenchFootL", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.03f, -0.65f), Vector3.zero, new Vector3(0.50f, 0.06f, 0.10f), benchMat);
        Make("BenchFootR", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.03f,  0.65f), Vector3.zero, new Vector3(0.50f, 0.06f, 0.10f), benchMat);
        Make("BenchPad", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(0.42f, 0.09f, 1.60f), leatherMat);
        // Folded white towel
        Make("GymTowel", PrimitiveType.Cube, benchGroup.transform, new Vector3(0f, 0.60f, -0.15f), new Vector3(0f, 10f, 0f), new Vector3(0.32f, 0.08f, 0.38f), towelMat);
        // Blue sports water bottle
        Make("ShakerBody", PrimitiveType.Cylinder, benchGroup.transform, new Vector3(0f, 0.70f, 0.40f), Vector3.zero, new Vector3(0.12f, 0.22f, 0.12f), bottleMat);
        Make("ShakerCap", PrimitiveType.Cylinder, benchGroup.transform, new Vector3(0f, 0.83f, 0.40f), Vector3.zero, new Vector3(0.13f, 0.05f, 0.13f), hexIronMat);

        // Potted gym plant on far left (as in reference image)
        GameObject plant = new GameObject("GymPlant");
        plant.transform.position = new Vector3(-2.4f, 0f, -2.8f);
        Make("Pot", PrimitiveType.Cylinder, plant.transform, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.50f, 0.45f, 0.50f), potMat);
        Make("Stem", PrimitiveType.Cylinder, plant.transform, new Vector3(0f, 1.25f, 0f), Vector3.zero, new Vector3(0.10f, 0.80f, 0.10f), plantMat);
        Make("Leaf1", PrimitiveType.Sphere, plant.transform, new Vector3(0.25f, 1.55f, 0.15f), new Vector3(25f, 30f, 0f), new Vector3(0.45f, 0.15f, 0.35f), plantMat);
        Make("Leaf2", PrimitiveType.Sphere, plant.transform, new Vector3(-0.25f, 1.70f, -0.15f), new Vector3(-20f, 60f, 0f), new Vector3(0.45f, 0.15f, 0.35f), plantMat);
        Make("Leaf3", PrimitiveType.Sphere, plant.transform, new Vector3(0.05f, 1.95f, 0.25f), new Vector3(15f, -40f, 0f), new Vector3(0.40f, 0.15f, 0.35f), plantMat);

        // Background Dumbbell Racks with Hex Dumbbells (from reference photo)
        MakeHexRack("RackLeft",  new Vector3(-2.3f, 0f, -0.6f), benchMat, hexIronMat, chromeMat);
        MakeHexRack("RackRight", new Vector3(-2.3f, 0f,  4.2f), benchMat, hexIronMat, chromeMat);

        // ---------------- 3D Muscular Character (Matching the image)
        GameObject lifter = new GameObject("LifterCharacter");
        lifter.transform.position = new Vector3(-0.45f, 0f, -0.15f);

        // Athletic Legs
        Make("LegL", PrimitiveType.Capsule, lifter.transform, new Vector3(-0.16f, 1.05f, 0.05f), Vector3.zero, new Vector3(0.28f, 0.55f, 0.28f), skinMat);
        Make("LegR", PrimitiveType.Capsule, lifter.transform, new Vector3( 0.16f, 1.05f, 0.05f), Vector3.zero, new Vector3(0.28f, 0.55f, 0.28f), skinMat);
        Make("SneakerL", PrimitiveType.Cube, lifter.transform, new Vector3(-0.16f, 0.10f, 0.10f), Vector3.zero, new Vector3(0.24f, 0.20f, 0.48f), hexIronMat);
        Make("SneakerR", PrimitiveType.Cube, lifter.transform, new Vector3( 0.16f, 0.10f, 0.10f), Vector3.zero, new Vector3(0.24f, 0.20f, 0.48f), hexIronMat);

        // Black Gym Shorts with White Athletic Racing Stripe
        GameObject shorts = Make("Shorts", PrimitiveType.Capsule, lifter.transform, new Vector3(0f, 1.62f, 0f), Vector3.zero, new Vector3(0.68f, 0.50f, 0.52f), shortsMat);
        Make("StripeL", PrimitiveType.Cube, shorts.transform, new Vector3(0.48f, 0f, 0f), Vector3.zero, new Vector3(0.04f, 0.85f, 0.08f), stripeMat);

        // Muscular Torso in Navy Tank Top
        GameObject torso = Make("Torso", PrimitiveType.Capsule, lifter.transform, new Vector3(0f, 2.50f, 0f), Vector3.zero, new Vector3(0.72f, 0.65f, 0.50f), tankMat);
        Make("ChestPecs", PrimitiveType.Cube, torso.transform, new Vector3(0f, 0.15f, 0.18f), Vector3.zero, new Vector3(0.65f, 0.35f, 0.25f), tankMat);

        // Neck and Head with Styled Hair
        Make("Neck", PrimitiveType.Cylinder, lifter.transform, new Vector3(0f, 3.15f, 0f), Vector3.zero, new Vector3(0.22f, 0.18f, 0.22f), skinMat);
        GameObject head = Make("Head", PrimitiveType.Sphere, lifter.transform, new Vector3(0f, 3.52f, 0.05f), Vector3.zero, new Vector3(0.44f, 0.50f, 0.44f), skinMat);
        // Styled dark hair layers (profile matches the reference image)
        Make("HairTop",   PrimitiveType.Sphere, head.transform, new Vector3(0f, 0.22f, -0.05f), new Vector3(-10f, 0f, 0f), new Vector3(1.05f, 0.65f, 1.15f), hairMat);
        Make("HairBack",  PrimitiveType.Sphere, head.transform, new Vector3(0f, 0.05f, -0.18f), Vector3.zero, new Vector3(1.02f, 0.70f, 0.85f), hairMat);
        Make("HairFront", PrimitiveType.Sphere, head.transform, new Vector3(0f, 0.24f,  0.15f), new Vector3(15f, 0f, 0f), new Vector3(0.95f, 0.45f, 0.75f), hairMat);

        // Left Arm (hanging naturally / resting)
        Make("ShoulderL", PrimitiveType.Sphere, lifter.transform, new Vector3(-0.46f, 2.85f, 0f), Vector3.zero, Vector3.one * 0.32f, skinMat);
        Make("UpperArmL", PrimitiveType.Cylinder, lifter.transform, new Vector3(-0.46f, 2.25f, 0f), Vector3.zero, new Vector3(0.22f, 0.55f, 0.22f), skinMat);
        Make("ForearmL",  PrimitiveType.Cylinder, lifter.transform, new Vector3(-0.46f, 1.65f, 0f), Vector3.zero, new Vector3(0.20f, 0.50f, 0.20f), skinMat);

        // Right Shoulder (Deltoid)
        Make("ShoulderR", PrimitiveType.Sphere, lifter.transform, new Vector3(0.46f, 2.85f, 0f), Vector3.zero, Vector3.one * 0.34f, skinMat);

        // ---------------- Active Right Arm (Controlled by EMG)
        GameObject arm = new GameObject("Arm");
        arm.transform.position = new Vector3(0f, 2.85f, -0.15f);

        // Upper Arm
        Make("UpperArm", PrimitiveType.Cylinder, arm.transform, new Vector3(0f, -0.48f, 0f), Vector3.zero, new Vector3(0.24f, 0.52f, 0.24f), skinMat);

        // Dynamic 3D Biceps Muscle (expands on contraction!)
        GameObject bicep = Make("BicepMuscle", PrimitiveType.Capsule, arm.transform, new Vector3(0f, -0.42f, 0.11f), new Vector3(10f, 0f, 0f), new Vector3(0.28f, 0.36f, 0.28f), skinMat);

        // Elbow Joint Pivot
        GameObject elbow = Empty("ElbowPivot", arm.transform, new Vector3(0f, -0.98f, 0f));
        Make("ElbowJointMesh", PrimitiveType.Sphere, elbow.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.24f, skinMat);

        // Forearm extending forward and down
        Make("Forearm", PrimitiveType.Cylinder, elbow.transform, new Vector3(0f, -0.45f, 0f), Vector3.zero, new Vector3(0.20f, 0.48f, 0.20f), skinMat);

        // Hand
        Make("Hand", PrimitiveType.Sphere, elbow.transform, new Vector3(0f, -0.96f, 0f), Vector3.zero, new Vector3(0.22f, 0.24f, 0.22f), skinMat);

        // 3D Hexagonal Dumbbell held in hand
        GameObject dumbbell = Empty("HexDumbbell", elbow.transform, new Vector3(0f, -0.96f, 0f));
        Make("Handle", PrimitiveType.Cylinder, dumbbell.transform, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.065f, 0.38f, 0.065f), chromeMat);
        // Hex dumbbell heads (Left and Right)
        MakeHexHead("HexHeadL", dumbbell.transform, new Vector3(-0.25f, 0f, 0f), hexIronMat);
        MakeHexHead("HexHeadR", dumbbell.transform, new Vector3( 0.25f, 0f, 0f), hexIronMat);

        // ---------------- 3D Holographic Ghost Target (Contour of Hex Dumbbell)
        GameObject ghostPivot = Empty("GhostPivot", arm.transform, new Vector3(0f, -0.98f, 0f));
        GameObject ghostDumbbell = Empty("GhostDumbbell", ghostPivot.transform, new Vector3(0f, -0.96f, 0f));
        Make("GhostBar", PrimitiveType.Cylinder, ghostDumbbell.transform, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.09f, 0.42f, 0.09f), ghostMat);
        GameObject ghostHeadL = MakeHexHead("GhostHeadL", ghostDumbbell.transform, new Vector3(-0.25f, 0f, 0f), ghostMat);
        MakeHexHead("GhostHeadR", ghostDumbbell.transform, new Vector3( 0.25f, 0f, 0f), ghostMat);

        // Renderer assigned for dynamic IN-ZONE glow
        Renderer ghostRenderer = ghostHeadL.GetComponent<Renderer>();

        // ---------------- High-Fidelity UI System (1:1 with reference photo)
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform ui = canvasGO.transform;

        // Top Bar
        Image topBar = Panel(ui, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-48f, 76f), BgDarkPanel, roundSprite);
        Label(topBar.transform, "Title", "EMG LIFT", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(280f, 56f), 34, TextAlignmentOptions.Left, Color.white, true);

        // Interactive Top Chips
        Button modeBtn  = MakeChipButton(topBar.transform, "ModeChip",  new Vector2(280f, 0f), 150f, ChipBlue,   "HOLD");
        Button levelBtn = MakeChipButton(topBar.transform, "LevelChip", new Vector2(450f, 0f), 160f, ChipOrange, "6 kg");
        Button inputBtn = MakeChipButton(topBar.transform, "InputChip", new Vector2(630f, 0f), 260f, ChipGreen,  "KEYBOARD");

        // Player Text (Right side: "P1  •  Round 2/3")
        TMP_Text playerTxt = Label(topBar.transform, "PlayerText", "P1  •  Round 2/3", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 0f), new Vector2(260f, 50f), 24, TextAlignmentOptions.Right, GreyText, false);

        // Window Control Buttons (Minimize -, Maximize □, Close ✕)
        Button minBtn   = MakeWindowButton(topBar.transform, "MinButton",   new Vector2(-150f, 0f), "-", 28, new Color(0.20f, 0.22f, 0.26f));
        Button maxBtn   = MakeWindowButton(topBar.transform, "MaxButton",   new Vector2(-95f,  0f), "□", 20, new Color(0.20f, 0.22f, 0.26f));
        Button closeBtn = MakeWindowButton(topBar.transform, "CloseButton", new Vector2(-40f,  0f), "✕", 22, new Color(0.85f, 0.20f, 0.20f));

        // Score Card (Top Left - identical to photo)
        Image scoreCard = Panel(ui, "ScoreCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -110f), new Vector2(400f, 260f), BgDarkPanel, roundSprite);
        Label(scoreCard.transform, "ScoreLabel", "SCORE", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(160f, 28f), 20, TextAlignmentOptions.TopLeft, GreyText, false);

        TMP_Text scoreValTxt = Label(scoreCard.transform, "ScoreText", "2,840", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -54f), new Vector2(240f, 85f), 68, TextAlignmentOptions.TopLeft, Color.white, true);

        // Yellow multiplier badge "x2"
        Image multBadge = Panel(scoreCard.transform, "MultiplierBadge", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -60f), new Vector2(98f, 54f), GoldYellow, roundSprite);
        TMP_Text multValTxt = Label(multBadge.transform, "MultiplierText", "x2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 30, TextAlignmentOptions.Center, new Color(0.08f, 0.08f, 0.08f), true);

        TMP_Text streakValTxt = Label(scoreCard.transform, "StreakText", "Streak: 6 in a row", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -160f), new Vector2(350f, 36f), 24, TextAlignmentOptions.TopLeft, StreakGold, true);
        TMP_Text zoneValTxt   = Label(scoreCard.transform, "ZoneText", "Time in zone: 74%", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -204f), new Vector2(350f, 36f), 24, TextAlignmentOptions.TopLeft, Color.white, false);

        // Effort Meter (Right side - vertical card with live "72%" & Target line)
        // Positioned down and offset so it never overlaps the timer ring
        Image effortCard = Panel(ui, "EffortCard", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, -80f), new Vector2(120f, 540f), BgDarkPanel, roundSprite);
        Label(effortCard.transform, "EffortTitle", "EFFORT", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(110f, 26f), 18, TextAlignmentOptions.Center, GreyText, true);

        TMP_Text effortPctTxt = Label(effortCard.transform, "EffortPercent", "72%", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(110f, 42f), 34, TextAlignmentOptions.Center, Color.white, true);

        // Effort Meter Track
        Image effortTrack = Panel(effortCard.transform, "Track", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TrackDark, roundSprite);
        effortTrack.rectTransform.offsetMin = new Vector2(32f, 26f);
        effortTrack.rectTransform.offsetMax = new Vector2(-32f, -92f);

        // Green Fill Bar
        Image effortFillBar = Panel(effortTrack.transform, "Fill", Vector2.zero, new Vector2(1f, 0.72f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, ChipGreen, roundSprite);

        // Translucent Target Band
        Image targetBandOverlay = Panel(effortTrack.transform, "TargetBand", Vector2.zero, new Vector2(1f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.25f), null);

        // Target Indicator Line ("— Target")
        RectTransform targetMarker = Rect(effortCard.transform, "TargetMarker", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(90f, 30f));
        Label(targetMarker, "TargetLabel", "— Target", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero, 18, TextAlignmentOptions.Left, ChipGreen, true);

        // Timer Ring (Far Right: Circular Disc with 23 s)
        RectTransform timerDisc = Rect(ui, "TimerDisc", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-35f, -120f), new Vector2(210f, 210f));
        Image timerBg = Panel(timerDisc, "TimerBg", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, BgDarkPanel, circleSprite);

        Image ringTrack = Panel(timerDisc, "RingTrack", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -20f), TrackDark, circleSprite);

        Image ring = Panel(timerDisc, "RingFill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -20f), NeonCyan, circleSprite);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;

        Image ringHole = Panel(timerDisc, "RingHole", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-54f, -54f), BgDarkPanel, circleSprite);

        TMP_Text timerValTxt = Label(timerDisc, "TimerText", "23", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(140f, 65f), 58, TextAlignmentOptions.Center, Color.white, true);
        TMP_Text secValTxt   = Label(timerDisc, "SecLabel", "s", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(60f, 30f), 24, TextAlignmentOptions.Center, Color.white, true);

        // Floating Bottom Hint Pill (Matching photo: "Hold steady! Next target: 120° in 4 s")
        Image hintPill = Panel(ui, "HintPill", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1050f, 76f), BgDarkPanel, roundSprite);
        TMP_Text hintValTxt = Label(hintPill.transform, "HintText", "Hold steady!  Next target: 120° in 4 s", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 28, TextAlignmentOptions.Center, Color.white, true);

        // Floating "IN ZONE" Badge (directly adjacent to target)
        Image inZoneBadge = Panel(ui, "InZoneBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 65f), new Vector2(180f, 52f), new Color(0.05f, 0.25f, 0.12f, 0.95f), roundSprite);
        TMP_Text inZoneTxt = Label(inZoneBadge.transform, "InZoneText", "IN ZONE", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 24, TextAlignmentOptions.Center, ChipGreen, true);

        // ---------------- ESC Pause / System Menu Modal Panel
        GameObject pauseModal = new GameObject("PauseModal", typeof(RectTransform));
        pauseModal.transform.SetParent(ui, false);
        var pauseModalRt = (RectTransform)pauseModal.transform;
        pauseModalRt.anchorMin = Vector2.zero;
        pauseModalRt.anchorMax = Vector2.one;
        pauseModalRt.sizeDelta = Vector2.zero;
        Image pauseDim = pauseModal.AddComponent<Image>();
        pauseDim.color = new Color(0f, 0f, 0f, 0.70f);

        Image pauseCard = Panel(pauseModal.transform, "PauseCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(550f, 440f), BgDarkPanel, roundSprite);
        Label(pauseCard.transform, "PauseTitle", "SYSTEM PAUSE", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(400f, 40f), 32, TextAlignmentOptions.Center, Color.white, true);
        Label(pauseCard.transform, "PauseSub", "SSRP Project 7: Real-Time EMG Weightlifting", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(480f, 26f), 18, TextAlignmentOptions.Center, GreyText, false);

        Button resumeBtn  = MakeMenuButton(pauseCard.transform, "ResumeBtn",  new Vector2(0f, 45f),  "Resume Game (ESC)", ChipBlue);
        Button fullscrBtn = MakeMenuButton(pauseCard.transform, "FullscrBtn", new Vector2(0f, -25f), "Toggle Fullscreen / Windowed", new Color(0.25f, 0.28f, 0.35f));
        Button restartBtn = MakeMenuButton(pauseCard.transform, "RestartBtn", new Vector2(0f, -95f), "Restart Round (R)", new Color(0.25f, 0.28f, 0.35f));
        Button quitBtn    = MakeMenuButton(pauseCard.transform, "QuitBtn",    new Vector2(0f, -165f), "Quit to Desktop", new Color(0.85f, 0.22f, 0.22f));

        // ---------------- Scripts and GameSystems Wiring
        GameObject sys = new GameObject("GameSystems");
        var receiver   = sys.AddComponent<UDPReceiver>();
        var armCtrl    = sys.AddComponent<ArmController>();
        var logger     = sys.AddComponent<GameLogger>();
        var windowCtrl = sys.AddComponent<WindowController>();
        var gm         = sys.AddComponent<GameManager>();

        armCtrl.elbowPivot   = elbow.transform;
        armCtrl.receiver     = receiver;
        armCtrl.bicepMuscle  = bicep.transform;

        logger.arm           = armCtrl;
        logger.receiver      = receiver;
        logger.subjectId     = "P1";

        gm.arm               = armCtrl;
        gm.ghostPivot        = ghostPivot.transform;
        gm.ghostRenderer     = ghostRenderer;
        gm.logger            = logger;
        gm.windowController  = windowCtrl;
        gm.cam               = cam;
        gm.lightRequired     = 0.60f;
        gm.heavyRequired     = 0.60f;

        gm.modeChipText      = modeBtn.GetComponentInChildren<TMP_Text>();
        gm.levelChipText     = levelBtn.GetComponentInChildren<TMP_Text>();
        gm.inputChipText     = inputBtn.GetComponentInChildren<TMP_Text>();
        gm.playerText        = playerTxt;

        gm.modeChipBtn       = modeBtn;
        gm.levelChipBtn      = levelBtn;
        gm.inputChipBtn      = inputBtn;
        gm.minBtn            = minBtn;
        gm.maxBtn            = maxBtn;
        gm.closeBtn          = closeBtn;

        gm.scoreText         = scoreValTxt;
        gm.multiplierText    = multValTxt;
        gm.streakText        = streakValTxt;
        gm.zoneText          = zoneValTxt;

        gm.effortPercentText = effortPctTxt;
        gm.effortFill        = effortFillBar.rectTransform;
        gm.targetBand        = targetBandOverlay.rectTransform;
        gm.targetMarkerLine  = targetMarker;

        gm.timerRing         = ring;
        gm.timerText         = timerValTxt;
        gm.secLabel          = secValTxt;

        gm.hintText          = hintValTxt;
        gm.inZoneText        = inZoneTxt;
        gm.pausePanel        = pauseModal;

        // Wire Pause Menu Buttons
        resumeBtn.onClick.AddListener(gm.ResumeGame);
        fullscrBtn.onClick.AddListener(windowCtrl.ToggleMaximize);
        restartBtn.onClick.AddListener(gm.RestartRound);
        quitBtn.onClick.AddListener(windowCtrl.QuitGame);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Weightlifting.unity");
        Selection.activeGameObject = sys;
        Debug.Log("[WeightliftingSceneBuilder] High-Fidelity 3D Weightlifting Scene built and saved to Assets/Scenes/Weightlifting.unity");
    }

    // ---------------- 3D Environment & Hex Dumbbell Helpers
    static GameObject MakeHexHead(string name, Transform parent, Vector3 localPos, Material mat)
    {
        // Creates a faceted hexagonal dumbbell head using overlapping rotated prisms
        GameObject head = new GameObject(name);
        head.transform.SetParent(parent, false);
        head.transform.localPosition = localPos;

        // Combine 3 prisms rotated at 0, 60, 120 degrees along Z to form a solid hexagonal head
        Make("HexA", PrimitiveType.Cube, head.transform, Vector3.zero, new Vector3(0f, 0f,   0f), new Vector3(0.18f, 0.44f, 0.50f), mat);
        Make("HexB", PrimitiveType.Cube, head.transform, Vector3.zero, new Vector3(0f, 0f,  60f), new Vector3(0.18f, 0.44f, 0.50f), mat);
        Make("HexC", PrimitiveType.Cube, head.transform, Vector3.zero, new Vector3(0f, 0f, 120f), new Vector3(0.18f, 0.44f, 0.50f), mat);

        return head;
    }

    static void MakeHexRack(string name, Vector3 pos, Material frameMat, Material hexMat, Material chromeMat)
    {
        GameObject rack = new GameObject(name);
        rack.transform.position = pos;

        Make("PostL", PrimitiveType.Cube, rack.transform, new Vector3(0f, 0.95f, -0.95f), Vector3.zero, new Vector3(0.10f, 1.9f, 0.10f), frameMat);
        Make("PostR", PrimitiveType.Cube, rack.transform, new Vector3(0f, 0.95f,  0.95f), Vector3.zero, new Vector3(0.10f, 1.9f, 0.10f), frameMat);
        Make("Base",  PrimitiveType.Cube, rack.transform, new Vector3(0f, 0.05f, 0f),     Vector3.zero, new Vector3(0.50f, 0.10f, 2.0f), frameMat);

        float[] shelfHeights = { 0.55f, 1.15f };
        for (int s = 0; s < shelfHeights.Length; s++)
        {
            Make("Shelf" + s, PrimitiveType.Cube, rack.transform, new Vector3(0f, shelfHeights[s], 0f), new Vector3(15f, 0f, 0f), new Vector3(0.42f, 0.05f, 1.95f), frameMat);
            float size = 0.35f - 0.05f * s;
            for (int d = 0; d < 4; d++)
            {
                float z = -0.66f + 0.44f * d;
                GameObject db = new GameObject($"Dumbbell_{s}_{d}");
                db.transform.SetParent(rack.transform, false);
                db.transform.localPosition = new Vector3(0.08f, shelfHeights[s] + size / 2f + 0.02f, z);

                Make("Bar", PrimitiveType.Cylinder, db.transform, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.24f, 0.05f), chromeMat);
                Make("PlL", PrimitiveType.Cylinder, db.transform, new Vector3(-0.14f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(size, 0.08f, size), hexMat);
                Make("PlR", PrimitiveType.Cylinder, db.transform, new Vector3( 0.14f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(size, 0.08f, size), hexMat);
            }
        }
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    static Material MakeMaterial(string name, Color color, bool transparent, float metallic = 0.0f, float smoothness = 0.5f, bool emissive = false, Color emissionColor = default)
    {
        string path = "Assets/Materials/" + name + ".mat";
        // Always rebuild, so a pink material from an earlier build is replaced
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);

        // URP project -> URP Lit; Built-in project -> Standard
        Shader shader = GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", color);

        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            Color eColor = emissionColor == default ? color : emissionColor;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", eColor);
        }

        if (transparent)
        {
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); // 1 = Transparent
                m.SetFloat("_Blend", 0f);   // 0 = Alpha
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (m.HasProperty("_Mode"))
            {
                m.SetFloat("_Mode", 3f); // 3 = Transparent
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Make(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localEuler;
        go.transform.localScale = localScale;

        Collider c = go.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);

        var ren = go.GetComponent<Renderer>();
        if (ren != null) ren.sharedMaterial = mat;

        return go;
    }

    static GameObject Empty(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }

    // ---------------- UI Creation Helpers
    static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        RectTransform rt = Rect(parent, name, anchorMin, anchorMax, pivot, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color, bool bold)
    {
        RectTransform rt = Rect(parent, name, anchorMin, anchorMax, pivot, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = color;
        if (bold) t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false;
        return t;
    }

    static Button MakeChipButton(Transform parent, string name, Vector2 pos, float width, Color color, string initialText)
    {
        Image chipImg = Panel(parent, name, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, new Vector2(width, 48f), color, roundSprite);
        Button btn = chipImg.gameObject.AddComponent<Button>();

        var colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.15f;
        colors.pressedColor = color * 0.85f;
        btn.colors = colors;

        Label(chipImg.transform, name + "Text", initialText, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Center, Color.white, true);
        return btn;
    }

    static Button MakeWindowButton(Transform parent, string name, Vector2 pos, string symbol, float fontSize, Color normalColor)
    {
        Image btnImg = Panel(parent, name, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(44f, 44f), normalColor, roundSprite);
        Button btn = btnImg.gameObject.AddComponent<Button>();

        var colors = btn.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = normalColor * 1.35f;
        colors.pressedColor = normalColor * 0.75f;
        btn.colors = colors;

        Label(btnImg.transform, "Symbol", symbol, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, TextAlignmentOptions.Center, Color.white, true);
        return btn;
    }

    static Button MakeMenuButton(Transform parent, string name, Vector2 anchoredPos, string label, Color color)
    {
        Image btnImg = Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(440f, 54f), color, roundSprite);
        Button btn = btnImg.gameObject.AddComponent<Button>();

        var colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        btn.colors = colors;

        Label(btnImg.transform, "Label", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 22, TextAlignmentOptions.Center, Color.white, true);
        return btn;
    }
}
