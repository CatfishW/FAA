using UnityEngine;
using System;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Represents a single simulated storm cell with position, size, intensity, and lifecycle.
    /// Handles growth, maturation, and dissipation phases.
    /// </summary>
    [Serializable]
    public class SimulatedStormCell
    {
        #region Identity
        
        /// <summary>Unique identifier for this cell</summary>
        public string CellId { get; private set; }
        
        /// <summary>Display name for debugging</summary>
        public string DisplayName { get; set; }
        
        #endregion

        #region Position and Size
        
        /// <summary>Cell center position in world coordinates (X, Z plane)</summary>
        public Vector2 Position { get; set; }
        
        /// <summary>Base altitude in feet MSL</summary>
        public float BaseAltitude { get; set; }
        
        /// <summary>Top altitude in feet MSL</summary>
        public float TopAltitude { get; set; }
        
        /// <summary>Current horizontal radius in world units</summary>
        public float Radius { get; private set; }
        
        /// <summary>Maximum radius this cell will reach at maturity</summary>
        public float MaxRadius { get; private set; }
        
        /// <summary>Movement velocity in world units per second</summary>
        public Vector2 Velocity { get; set; }
        
        #endregion

        #region Intensity
        
        /// <summary>Current intensity level</summary>
        public IntensityLevel Intensity { get; private set; }
        
        /// <summary>Target intensity (for dynamic intensity changes)</summary>
        public IntensityLevel TargetIntensity { get; set; }
        
        /// <summary>Intensity as normalized value (0-1)</summary>
        public float NormalizedIntensity => (float)Intensity / 4f;
        
        /// <summary>Core intensity multiplier (higher at center)</summary>
        public float CoreIntensityMultiplier { get; set; } = 1.2f;
        
        #endregion

        #region Lifecycle
        
        /// <summary>Current lifecycle phase</summary>
        public CellPhase Phase { get; private set; }
        
        /// <summary>Total lifetime in seconds</summary>
        public float Lifetime { get; private set; }
        
        /// <summary>Current age in seconds</summary>
        public float Age { get; private set; }
        
        /// <summary>Normalized age (0-1)</summary>
        public float NormalizedAge => Lifetime > 0 ? Age / Lifetime : 0f;
        
        /// <summary>Phase progress (0-1 within current phase)</summary>
        public float PhaseProgress { get; private set; }
        
        /// <summary>Growth phase duration fraction</summary>
        public float GrowthPhaseFraction { get; set; } = 0.25f;
        
        /// <summary>Mature phase duration fraction</summary>
        public float MaturePhaseFraction { get; set; } = 0.5f;
        
        /// <summary>Whether this cell has completed its lifecycle</summary>
        public bool IsExpired => Phase == CellPhase.Expired;
        
        /// <summary>Whether this cell is currently active (not expired)</summary>
        public bool IsActive => Phase != CellPhase.Expired;
        
        #endregion

        #region Visual Properties
        
        /// <summary>Current opacity (affected by phase)</summary>
        public float Opacity { get; private set; } = 1f;
        
        /// <summary>Turbulence intensity (0-1)</summary>
        public float TurbulenceIntensity { get; set; }
        
        /// <summary>Lightning activity level (0-1)</summary>
        public float LightningActivity { get; set; }
        
        /// <summary>Precipitation rate (0-1)</summary>
        public float PrecipitationRate { get; set; }
        
        /// <summary>Anvil spread (for mature thunderstorms)</summary>
        public float AnvilSpread { get; set; }
        
        #endregion

        #region Events
        
        /// <summary>Fired when cell enters a new phase</summary>
        public event Action<SimulatedStormCell, CellPhase> OnPhaseChanged;
        
        /// <summary>Fired when intensity changes</summary>
        public event Action<SimulatedStormCell, IntensityLevel, IntensityLevel> OnIntensityChanged;
        
        /// <summary>Fired when cell expires</summary>
        public event Action<SimulatedStormCell> OnExpired;
        
        #endregion

        #region Constructors
        
        /// <summary>
        /// Create a new storm cell with specified parameters
        /// </summary>
        public SimulatedStormCell(
            Vector2 position,
            float maxRadius,
            IntensityLevel intensity,
            float lifetime,
            float baseAltitude,
            float topAltitude)
        {
            CellId = Guid.NewGuid().ToString("N").Substring(0, 8);
            DisplayName = $"Cell_{CellId}";
            
            Position = position;
            MaxRadius = maxRadius;
            Radius = 0f; // Starts at zero, grows over time
            
            Intensity = intensity;
            TargetIntensity = intensity;
            
            Lifetime = lifetime;
            Age = 0f;
            Phase = CellPhase.Forming;
            
            BaseAltitude = baseAltitude;
            TopAltitude = topAltitude;
            
            Velocity = Vector2.zero;
            
            // Initialize effects based on intensity
            UpdateEffectsForIntensity();
        }
        
        /// <summary>
        /// Create a storm cell from a scenario preset at a random position
        /// </summary>
        public static SimulatedStormCell CreateFromPreset(WeatherScenarioPreset preset, Vector2 position)
        {
            var intensity = preset.GetRandomIntensity();
            var cell = new SimulatedStormCell(
                position,
                preset.GetRandomRadius(),
                intensity,
                preset.GetRandomLifetime(),
                preset.GetRandomBaseAltitude(),
                preset.GetTopAltitudeForIntensity(intensity)
            );
            
            cell.Velocity = preset.GetRandomVelocity();
            cell.GrowthPhaseFraction = preset.growthPhaseFraction;
            cell.MaturePhaseFraction = preset.maturePhaseFraction;
            
            return cell;
        }
        
        #endregion

        #region Update Methods
        
        /// <summary>
        /// Update the storm cell simulation
        /// </summary>
        /// <param name="deltaTime">Time since last update in seconds</param>
        /// <param name="enableDynamicIntensity">Whether intensity can change over time</param>
        /// <param name="intensityChangeRate">Rate of intensity changes</param>
        public void Update(float deltaTime, bool enableDynamicIntensity = true, float intensityChangeRate = 0.3f)
        {
            if (Phase == CellPhase.Expired)
                return;
            
            // Update age
            Age += deltaTime;
            
            // Update position based on velocity
            Position += Velocity * deltaTime;
            
            // Update lifecycle phase
            UpdatePhase();
            
            // Update radius based on phase
            UpdateRadius();
            
            // Update opacity based on phase
            UpdateOpacity();
            
            // Update intensity if dynamic
            if (enableDynamicIntensity)
            {
                UpdateDynamicIntensity(deltaTime, intensityChangeRate);
            }
            
            // Update altitude (anvil spread for mature storms)
            UpdateAltitude();
            
            // Update visual effects
            UpdateEffectsForIntensity();
        }
        
        private void UpdatePhase()
        {
            float normalizedAge = NormalizedAge;
            CellPhase previousPhase = Phase;
            
            float growthEnd = GrowthPhaseFraction;
            float matureEnd = GrowthPhaseFraction + MaturePhaseFraction;
            
            if (normalizedAge < growthEnd)
            {
                Phase = CellPhase.Growing;
                PhaseProgress = normalizedAge / GrowthPhaseFraction;
            }
            else if (normalizedAge < matureEnd)
            {
                Phase = CellPhase.Mature;
                PhaseProgress = (normalizedAge - growthEnd) / MaturePhaseFraction;
            }
            else if (normalizedAge < 1f)
            {
                Phase = CellPhase.Dissipating;
                float dissipationFraction = 1f - matureEnd;
                PhaseProgress = (normalizedAge - matureEnd) / dissipationFraction;
            }
            else
            {
                Phase = CellPhase.Expired;
                PhaseProgress = 1f;
                OnExpired?.Invoke(this);
            }
            
            if (Phase != previousPhase)
            {
                OnPhaseChanged?.Invoke(this, Phase);
            }
        }
        
        private void UpdateRadius()
        {
            switch (Phase)
            {
                case CellPhase.Forming:
                    Radius = 0f;
                    break;
                    
                case CellPhase.Growing:
                    // Ease-out growth curve
                    float growthCurve = 1f - Mathf.Pow(1f - PhaseProgress, 2f);
                    Radius = MaxRadius * growthCurve;
                    break;
                    
                case CellPhase.Mature:
                    // Slight pulsing at maturity
                    float pulse = 1f + 0.05f * Mathf.Sin(Age * 0.5f);
                    Radius = MaxRadius * pulse;
                    break;
                    
                case CellPhase.Dissipating:
                    // Gradual shrinking with fragmentation
                    float dissipationCurve = 1f - Mathf.Pow(PhaseProgress, 1.5f);
                    Radius = MaxRadius * dissipationCurve;
                    break;
                    
                case CellPhase.Expired:
                    Radius = 0f;
                    break;
            }
        }
        
        private void UpdateOpacity()
        {
            switch (Phase)
            {
                case CellPhase.Forming:
                    Opacity = 0f;
                    break;
                    
                case CellPhase.Growing:
                    // Fade in during growth
                    Opacity = Mathf.SmoothStep(0f, 1f, PhaseProgress);
                    break;
                    
                case CellPhase.Mature:
                    Opacity = 1f;
                    break;
                    
                case CellPhase.Dissipating:
                    // Fade out during dissipation
                    Opacity = Mathf.SmoothStep(1f, 0f, PhaseProgress);
                    break;
                    
                case CellPhase.Expired:
                    Opacity = 0f;
                    break;
            }
        }
        
        private void UpdateDynamicIntensity(float deltaTime, float changeRate)
        {
            // During mature phase, intensity may fluctuate
            if (Phase == CellPhase.Mature && UnityEngine.Random.value < changeRate * deltaTime * 0.1f)
            {
                // Small random changes
                int change = UnityEngine.Random.Range(-1, 2);
                int newIntensity = Mathf.Clamp((int)Intensity + change, 1, 4);
                SetIntensity((IntensityLevel)newIntensity);
            }
            
            // Intensity decreases during dissipation
            if (Phase == CellPhase.Dissipating && PhaseProgress > 0.5f)
            {
                if (Intensity > IntensityLevel.Light && UnityEngine.Random.value < deltaTime * 0.5f)
                {
                    SetIntensity(Intensity - 1);
                }
            }
        }
        
        private void UpdateAltitude()
        {
            // Anvil spread increases during mature phase for intense storms
            if (Phase == CellPhase.Mature && Intensity >= IntensityLevel.Heavy)
            {
                AnvilSpread = Mathf.Lerp(AnvilSpread, 0.3f, 0.01f);
            }
            else
            {
                AnvilSpread = Mathf.Lerp(AnvilSpread, 0f, 0.02f);
            }
        }
        
        private void UpdateEffectsForIntensity()
        {
            // Turbulence scales with intensity
            TurbulenceIntensity = NormalizedIntensity * (Phase == CellPhase.Mature ? 1f : 0.5f);
            
            // Lightning only in moderate+ intensity during mature phase
            if (Phase == CellPhase.Mature && Intensity >= IntensityLevel.Moderate)
            {
                LightningActivity = (NormalizedIntensity - 0.25f) * 1.33f; // 0 at moderate, 1 at extreme
            }
            else
            {
                LightningActivity = 0f;
            }
            
            // Precipitation scales with intensity
            PrecipitationRate = NormalizedIntensity * Opacity;
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Set the intensity level
        /// </summary>
        public void SetIntensity(IntensityLevel newIntensity)
        {
            if (newIntensity != Intensity)
            {
                var oldIntensity = Intensity;
                Intensity = newIntensity;
                OnIntensityChanged?.Invoke(this, oldIntensity, newIntensity);
                UpdateEffectsForIntensity();
            }
        }
        
        /// <summary>
        /// Get the density value at a specific position relative to cell center
        /// </summary>
        /// <param name="worldPosition">Position in world coordinates (X, Z)</param>
        /// <returns>Density value (0-1)</returns>
        public float GetDensityAt(Vector2 worldPosition)
        {
            if (Radius <= 0f || Opacity <= 0f)
                return 0f;
            
            float distance = Vector2.Distance(worldPosition, Position);
            
            if (distance > Radius)
                return 0f;
            
            // Smooth falloff from center
            float normalizedDist = distance / Radius;
            float density = 1f - Mathf.Pow(normalizedDist, 2f);
            
            // Apply core intensity multiplier near center
            if (normalizedDist < 0.3f)
            {
                density *= CoreIntensityMultiplier;
            }
            
            // Apply opacity and intensity
            density *= Opacity * NormalizedIntensity;
            
            return Mathf.Clamp01(density);
        }
        
        /// <summary>
        /// Get the density value at a 3D position
        /// </summary>
        public float GetDensityAt3D(Vector3 worldPosition)
        {
            // Check horizontal distance
            Vector2 horizontalPos = new Vector2(worldPosition.x, worldPosition.z);
            float horizontalDensity = GetDensityAt(horizontalPos);
            
            if (horizontalDensity <= 0f)
                return 0f;
            
            // Check vertical bounds
            float altitude = worldPosition.y;
            if (altitude < BaseAltitude || altitude > TopAltitude)
                return 0f;
            
            // Vertical density profile (denser in middle, less at edges)
            float verticalRange = TopAltitude - BaseAltitude;
            float normalizedAlt = (altitude - BaseAltitude) / verticalRange;
            
            // Asymmetric profile: denser at lower-middle, tapering at top
            float verticalDensity;
            if (normalizedAlt < 0.4f)
            {
                // Lower portion - moderate density
                verticalDensity = Mathf.SmoothStep(0.3f, 1f, normalizedAlt / 0.4f);
            }
            else if (normalizedAlt < 0.7f)
            {
                // Core - maximum density
                verticalDensity = 1f;
            }
            else
            {
                // Upper portion - tapering with anvil spread
                float upperProgress = (normalizedAlt - 0.7f) / 0.3f;
                verticalDensity = Mathf.SmoothStep(1f, 0f, upperProgress);
                
                // Anvil spread widens the top
                if (AnvilSpread > 0f && upperProgress > 0.5f)
                {
                    // Slightly increase horizontal reach at top
                    verticalDensity *= (1f + AnvilSpread);
                }
            }
            
            return horizontalDensity * verticalDensity;
        }
        
        /// <summary>
        /// Check if a position is within this cell's bounds
        /// </summary>
        public bool ContainsPosition(Vector2 worldPosition)
        {
            return Vector2.Distance(worldPosition, Position) <= Radius;
        }
        
        /// <summary>
        /// Get bounding box for this cell
        /// </summary>
        public Bounds GetBounds()
        {
            Vector3 center = new Vector3(Position.x, (BaseAltitude + TopAltitude) * 0.5f, Position.y);
            Vector3 size = new Vector3(Radius * 2f, TopAltitude - BaseAltitude, Radius * 2f);
            return new Bounds(center, size);
        }
        
        /// <summary>
        /// Force expire this cell immediately
        /// </summary>
        public void ForceExpire()
        {
            Phase = CellPhase.Expired;
            Opacity = 0f;
            Radius = 0f;
            OnExpired?.Invoke(this);
        }
        
        #endregion

        public override string ToString()
        {
            return $"[{DisplayName}] Phase={Phase}, Intensity={Intensity}, Radius={Radius:F0}, Age={Age:F1}/{Lifetime:F1}s";
        }
    }

    /// <summary>
    /// Lifecycle phases of a storm cell
    /// </summary>
    public enum CellPhase
    {
        /// <summary>Initial formation, not yet visible</summary>
        Forming,
        
        /// <summary>Growing in size and intensity</summary>
        Growing,
        
        /// <summary>At peak size and intensity</summary>
        Mature,
        
        /// <summary>Weakening and shrinking</summary>
        Dissipating,
        
        /// <summary>Lifecycle complete, ready for removal</summary>
        Expired
    }
}
