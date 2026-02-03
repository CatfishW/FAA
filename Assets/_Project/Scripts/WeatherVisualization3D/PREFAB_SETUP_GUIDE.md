# Weather Visualization Prefab Setup Guide

## Generated Assets

### AI-Generated Textures (via Nano Banana Pro)
Located in `Assets/_Project/Textures/WeatherVisualization/`:
- **LightningBolt.png** - Jagged electric bolt texture for lightning effects
- **RainDrop.png** - Elongated water droplet texture for rain particles  
- **Snowflake.png** - Hexagonal crystalline snowflake texture

### Materials
Located in `Assets/_Project/Materials/WeatherVisualization/`:
- **LightningMaterial.mat** - Uses LightningBolt.png, bright yellow-white color
- **RainMaterial.mat** - Uses RainDrop.png, blue-tinted transparent
- **SnowMaterial.mat** - Uses Snowflake.png, white transparent

### Editor Tools Created
Located in `Assets/_Project/Scripts/WeatherVisualization3D/Editor/`:

1. **WeatherMaterialSetup.cs**
   - Menu: Tools > Weather Visualization > Setup Materials
   - Automatically configures all materials with proper textures and blend modes

2. **WeatherPrefabCreator.cs**
   - Menu: Tools > Weather Visualization > Create Prefabs
   - Creates LightningBolt, RainParticles, and SnowParticles prefabs with proper settings

## Setup Instructions

### Step 1: Setup Materials
1. In Unity Editor, go to **Tools > Weather Visualization > Setup Materials**
2. This will configure all materials with:
   - Proper texture references
   - Alpha blending for transparency
   - Correct render queues
   - Appropriate colors

### Step 2: Create Prefabs
1. Go to **Tools > Weather Visualization > Create Prefabs**
2. This creates three prefabs in `Assets/_Project/Prefabs/WeatherVisualization/`:

#### LightningBolt.prefab
- Single burst particle system
- Stretch render mode for bolt appearance
- Duration: 1 second
- Max particles: 10

#### RainParticles.prefab
- Continuous emission
- 5000 max particles
- 1000 particles/second emission rate
- Box shape (100x100x100 units)
- Vertical velocity: -20 (falling down)

#### SnowParticles.prefab
- Continuous emission
- 2000 max particles
- 500 particles/second emission rate
- Box shape (100x100x100 units)
- Gentle falling with noise drift
- Billboard render mode

### Step 3: Assign Prefabs to Components
1. Select **WeatherVisualization3D** object in scene
2. Find the child objects:
   - **VolumetricLightning** - Assign LightningBolt prefab
   - **PrecipitationVFX** - Assign RainParticles and SnowParticles prefabs
3. The prefabs will be instantiated at runtime based on weather conditions

## Testing Prefabs

### To test individual prefabs:
1. Drag prefab into scene
2. Enter Play mode
3. Adjust position to camera view
4. Check particle visibility and movement

### Expected Visual Results:
- **Lightning**: Bright yellow-white jagged bolts, single flash
- **Rain**: Blue-tinted streaks falling vertically
- **Snow**: White hexagonal crystals drifting gently

## Troubleshooting

### If particles don't appear:
1. Check material has texture assigned
2. Verify particle system is playing (Play On Awake enabled)
3. Check camera position relative to particles
4. Ensure materials use Alpha blending (not Opaque)

### If textures are missing:
1. Run Setup Materials tool again
2. Check textures are in Textures folder
3. Verify texture import settings (not compressed too much)

## Next Steps

1. Run the Setup Materials menu item
2. Run the Create Prefabs menu item
3. Assign prefabs to your weather components
4. Test in Play mode
5. Adjust particle settings as needed for your scene scale
