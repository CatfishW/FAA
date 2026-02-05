# UI Toolkit Quick Reference

## CSS Classes Quick Lookup

### Menu Containers
```css
.radial-menu-container      /* Basic menu root */
.adv-menu-root              /* Advanced menu root */
```

### Segments
```css
.segment-container          /* Basic segment */
.segment-icon               /* Basic icon */
.segment-label              /* Basic label */
.segment-background         /* Basic background */
.segment-glow               /* Hover glow */
.segment-selected           /* Selected state */

.adv-main-segment           /* Advanced main segment */
.adv-main-icon              /* Advanced main icon */
.adv-main-name              /* Advanced main label */
.adv-sub-segment            /* Advanced sub-segment */
.adv-sub-icon               /* Advanced sub icon */
.adv-sub-name               /* Advanced sub label */
```

### Center Panel
```css
.center-panel               /* Basic center info */
.center-title               /* Basic title */
.center-description         /* Basic description */
.center-target              /* Target label */

.adv-center-info            /* Advanced center info */
.adv-center-title           /* Advanced title */
.adv-center-subtitle        /* Advanced subtitle */
```

### Effects
```css
.adv-ring-background        /* Ring effect */
.adv-ripple-container       /* Ripples */
.adv-gesture-indicator      /* Gesture visual */
.particle-container         /* Particles */
.particle                   /* Single particle */
```

## Design Tokens

### Colors (CSS Variables)
```css
--color-weather: rgb(51, 204, 255)        /* Cyan */
--color-traffic: rgb(255, 153, 51)        /* Orange */
--color-indicators: rgb(102, 230, 102)    /* Green */
--color-symbology: rgb(230, 102, 230)     /* Purple */
--color-vision: rgb(255, 204, 51)         /* Yellow */
--color-system: rgb(180, 180, 180)        /* Gray */

--color-bg-primary: rgba(20, 25, 30, 0.95)
--color-bg-secondary: rgba(40, 45, 55, 0.9)
--color-text-primary: rgba(255, 255, 255, 0.95)
--color-text-secondary: rgba(200, 210, 220, 0.85)
--color-text-tertiary: rgba(150, 160, 170, 0.7)
```

### Spacing
```css
--spacing-xs: 2px
--spacing-sm: 4px
--spacing-md: 8px
--spacing-lg: 12px
--spacing-xl: 16px
--spacing-2xl: 24px
```

### Font Sizes
```css
--font-size-xs: 8px
--font-size-sm: 9px
--font-size-md: 11px
--font-size-lg: 14px
--font-size-xl: 18px
```

### Transitions
```css
--transition-fast: 0.1s
--transition-base: 0.2s
--transition-slow: 0.3s
```

## Utility Classes

### Layout
```css
.flex-center                /* Center flex */
.flex-column                /* Column layout */
.flex-row                   /* Row layout */
.absolute                   /* Absolute position */
.relative                   /* Relative position */
```

### Visibility
```css
.hidden                     /* display: none */
.visible                    /* display: flex */
.no-pointer                 /* pointer-events: none */
.pointer-all                /* pointer-events: auto */
```

### Text
```css
.text-center                /* Center text */
.text-left                  /* Left text */
.text-right                 /* Right text */
.text-bold                  /* Bold font */
.text-italic                /* Italic font */
.text-sm                    /* Small font */
.text-md                    /* Medium font */
.text-lg                    /* Large font */
```

### Colors
```css
.text-primary               /* Primary text color */
.text-secondary             /* Secondary text color */
.text-tertiary              /* Tertiary text color */
.text-accent                /* Accent color */
.bg-primary                 /* Primary background */
.bg-secondary               /* Secondary background */
.bg-tertiary                /* Tertiary background */
```

### Spacing
```css
.m-0, .m-xs, .m-sm, .m-md, .m-lg      /* Margin utilities */
.p-0, .p-xs, .p-sm, .p-md, .p-lg      /* Padding utilities */
```

## Theme Classes

Apply to root element:
```css
.theme-light                /* Light theme */
.high-contrast              /* High contrast mode */
.reduced-motion             /* Reduced motion */
```

Default is dark theme. Apply others as needed:
```csharp
root.AddToClassList("theme-light");
root.AddToClassList("high-contrast");
root.AddToClassList("reduced-motion");
```

## Using the Theme Manager

```csharp
// Get instance
var themeManager = RadialMenuThemeManager.Instance;

// Toggle dark mode
themeManager.ToggleDarkMode();

// Set high contrast
themeManager.SetHighContrast(true);

// Set reduced motion
themeManager.SetReducedMotion(true);

// Check current state
bool isDark = themeManager.IsDarkModeEnabled;
```

## Common Patterns

### Styling a Custom Element
```csharp
var element = new VisualElement();
element.AddToClassList("my-element");

// In USS:
.my-element {
    width: 100px;
    height: 100px;
    background-color: var(--color-bg-secondary);
    padding: var(--spacing-md);
    border-radius: var(--radius-md);
}
```

### Responsive Styles
```css
/* Default (any size) */
.my-element {
    font-size: var(--font-size-md);
}

/* Tablets and smaller */
@media screen and (max-width: 1366px) {
    .my-element {
        font-size: var(--font-size-sm);
    }
}

/* 4K monitors */
@media screen and (min-width: 2560px) {
    .my-element {
        font-size: var(--font-size-lg);
    }
}
```

### Hover/Active States
```css
.button {
    background-color: var(--color-bg-secondary);
    transition-property: background-color, scale;
    transition-duration: var(--transition-base);
}

.button:hover {
    background-color: var(--color-bg-tertiary);
    scale: 1.05;
}

.button:active {
    scale: 0.95;
}
```

### Animation
```css
.element {
    opacity: 0;
    scale: 0;
    transition-property: opacity, scale;
    transition-duration: var(--transition-slow);
    transition-timing-function: ease-out;
}

.element.show {
    opacity: 1;
    scale: 1;
}
```

## File Locations

```
Assets/Resources/VoiceControl/
├── SharedDesignSystem.uss       ← Design tokens
├── RadialMenuStyles.uss         ← Basic menu styles
├── AdvancedRadialMenuStyles.uss ← Advanced menu styles
├── BasicRadialMenu.uxml         ← Basic menu layout
├── AdvancedRadialMenu.uxml      ← Advanced menu layout
├── UITOOLKIT_SETUP_SUMMARY.md   ← Setup checklist
└── (this file)

Assets/_Project/Scripts/VoiceControl/UI/
├── UIToolkitRadialMenu.cs
├── UIToolkitRadialMenuAdvanced.cs
├── UIToolkitRadialMenuTester.cs
├── RadialMenuThemeManager.cs
└── UI_TOOLKIT_SETUP_GUIDE.md    ← Full documentation
```

## Tips

1. **Always use CSS variables** for colors, spacing, and transitions
2. **Import SharedDesignSystem.uss** at the top of custom USS files
3. **Use media queries** for responsive design instead of script logic
4. **Apply transitions in CSS**, not in code
5. **Use utility classes** for common patterns
6. **Test with theme classes** enabled for accessibility
7. **Check high-contrast mode** for visibility issues

## Common Errors

| Error | Solution |
|-------|----------|
| Styles not applying | Check USS import, verify class names (case-sensitive) |
| Colors not updating | Use CSS variables, not inline colors |
| Transitions too fast/slow | Adjust `--transition-*` variable |
| Layout broken on other resolutions | Add media query breakpoints |
| Text hard to read | Apply `high-contrast` class for testing |

## Resources

- Full Guide: `UI_TOOLKIT_SETUP_GUIDE.md`
- Unity Docs: https://docs.unity3d.com/Manual/UIE-index.html
- USS Reference: https://docs.unity3d.com/Manual/UIE-USS-SelectorTypes.html
