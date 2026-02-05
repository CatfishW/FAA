# UI Toolkit Radial Menu Setup Guide

## Overview

This project uses Unity's UI Toolkit to create responsive, modern radial menus for voice command control. The implementation includes both a basic radial menu and an advanced hierarchical menu with gesture support.

## Project Structure

### Stylesheets (USS)

- **SharedDesignSystem.uss** - Common design tokens, variables, and utility classes used across all menus
- **RadialMenuStyles.uss** - Styles for the basic radial menu
- **AdvancedRadialMenuStyles.uss** - Styles for the advanced hierarchical radial menu

### Templates (UXML)

- **BasicRadialMenu.uxml** - Layout template for the basic radial menu
- **AdvancedRadialMenu.uxml** - Layout template for the advanced radial menu

### Scripts

- **UIToolkitRadialMenu.cs** - Basic radial menu implementation
- **UIToolkitRadialMenuAdvanced.cs** - Advanced hierarchical menu with sub-menus
- **UIToolkitRadialMenuTester.cs** - Testing utilities for radial menus
- **RadialMenuThemeManager.cs** - Theme switching and customization manager

## Design System

### Color Palette

The shared design system defines semantic color variables:

```css
--color-weather: rgb(51, 204, 255)      /* Cyan - Weather radar */
--color-traffic: rgb(255, 153, 51)       /* Orange - Traffic radar */
--color-indicators: rgb(102, 230, 102)   /* Green - Indicator system */
--color-symbology: rgb(230, 102, 230)    /* Purple - Symbology */
--color-vision: rgb(255, 204, 51)        /* Yellow - Vision briefing */
--color-system: rgb(180, 180, 180)       /* Gray - System */
```

### Spacing Scale

```css
--spacing-xs: 2px
--spacing-sm: 4px
--spacing-md: 8px
--spacing-lg: 12px
--spacing-xl: 16px
--spacing-2xl: 24px
```

### Typography

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

## CSS Classes

### Basic Menu Classes

- `.radial-menu-container` - Root menu container
- `.segment-container` - Individual menu segment
- `.segment-background` - Segment background fill
- `.segment-glow` - Hover glow effect
- `.segment-icon` - Segment icon element
- `.segment-label` - Segment text label
- `.center-panel` - Center info panel
- `.center-title` - Title in center panel
- `.center-description` - Description text
- `.center-target` - Target identifier text

### Advanced Menu Classes

- `.adv-menu-root` - Root advanced menu container
- `.adv-ring-background` - Ring background effect
- `.adv-ripple-container` - Ripple effect container
- `.adv-gesture-indicator` - Gesture recognition visual
- `.adv-main-segment` - Main category segment
- `.adv-main-bg` - Main segment background
- `.adv-main-icon-container` - Icon container
- `.adv-main-icon` - Main segment icon
- `.adv-main-name` - Main segment name
- `.adv-sub-segment` - Sub-menu segment
- `.adv-sub-bg` - Sub segment background
- `.adv-sub-icon` - Sub segment icon
- `.adv-sub-name` - Sub segment name
- `.adv-center-info` - Advanced center info panel
- `.adv-center-title` - Advanced center title
- `.adv-center-subtitle` - Advanced center subtitle

### Utility Classes

- `.flex-center` - Flex center alignment
- `.flex-column` - Flex column layout
- `.flex-row` - Flex row layout
- `.absolute` - Absolute positioning
- `.relative` - Relative positioning
- `.hidden` - Display none
- `.visible` - Display flex
- `.no-pointer` - Disable pointer events
- `.pointer-all` - Enable pointer events
- `.text-center` - Center text alignment
- `.text-bold` - Bold font weight
- `.text-primary` - Primary text color
- `.bg-primary` - Primary background color
- `.m-md`, `.p-lg`, etc. - Spacing utilities

## Theme Support

### Built-in Themes

1. **Dark Mode** (default)
   - Optimized for dark environments and reduced eye strain
   - Uses dark backgrounds with light text

2. **Light Mode**
   - Use `theme-light` class on root element
   - Inverted colors for bright environments

3. **High Contrast**
   - Use `high-contrast` class for accessibility
   - Enhanced border visibility and text contrast

4. **Reduced Motion**
   - Use `reduced-motion` class for accessibility preferences
   - Disables animations and transitions

### Using the Theme Manager

```csharp
// Get the theme manager
var themeManager = RadialMenuThemeManager.Instance;

// Toggle dark mode
themeManager.ToggleDarkMode();

// Set high contrast
themeManager.SetHighContrast(true);

// Set reduced motion preference
themeManager.SetReducedMotion(true);
```

## Responsive Design

Styles automatically adjust for different screen resolutions:

- **2560px+** (4K) - Larger elements and fonts
- **1920px** (1080p) - Standard desktop size
- **1366px** (iPad) - Tablet-optimized sizes
- Usage: `@media screen and (max-width: 1920px) { ... }`

## Animation System

### Transitions

Menus use CSS transitions for smooth animations:

```css
transition-property: scale, opacity;
transition-duration: 0.2s;
transition-timing-function: ease-out;
```

### Keyframe Animations

Standard animations defined:
- `expand-in` - Expanding menu open animation
- `collapse-out` - Collapsing menu close animation

## Best Practices

### 1. Using Colors

Always use CSS variables instead of hardcoding colors:
```css
/* ✓ Good */
color: var(--color-text-primary);

/* ✗ Bad */
color: rgba(255, 255, 255, 0.95);
```

### 2. Spacing

Use the spacing scale for consistent padding/margins:
```css
/* ✓ Good */
padding: var(--spacing-md);
margin: var(--spacing-lg);

/* ✗ Bad */
padding: 8px;
margin: 12px;
```

### 3. Typography

Use semantic font-size variables:
```css
/* ✓ Good */
font-size: var(--font-size-md);

/* ✗ Bad */
font-size: 11px;
```

### 4. Transitions

Use CSS transitions for animations, not inline scripting:
```css
/* ✓ Good */
transition-property: scale, opacity;
transition-duration: var(--transition-base);
```

## Accessibility Features

### Keyboard Navigation
- Tab through menu segments
- Enter/Space to select
- Escape to close menu

### Screen Reader Support
- All interactive elements have labels
- Semantic HTML-like structure
- Text descriptions for icons

### High Contrast Support
- Enhanced borders and colors
- 7:1 contrast ratio minimum
- Media query: `@media (prefers-contrast: high)`

### Reduced Motion Support
- Respects user's motion preferences
- Media query: `@media (prefers-reduced-motion: reduce)`
- Disables non-essential animations

## Performance Notes

1. **CSS Variables** - Efficiently updates theme colors at runtime
2. **Transitions** - Used instead of JavaScript animations where possible
3. **Flex Layout** - Responsive without complex calculations
4. **Media Queries** - Client-side responsive design

## Customization

### Modifying Colors

Edit `SharedDesignSystem.uss` to change primary colors:

```css
:root {
    --color-weather: rgb(51, 204, 255);  /* Change here */
    /* ... */
}
```

### Changing Animations

Modify transition durations in style sheets:

```css
.segment-container {
    transition-duration: 0.3s;  /* Increase for slower animation */
}
```

### Adding Custom Themes

Add new theme classes in USS:

```css
.theme-custom {
    --color-bg-primary: rgb(20, 30, 40);
    --color-text-primary: rgb(200, 220, 240);
    /* ... override variables ... */
}
```

Then apply in code:
```csharp
root.AddToClassList("theme-custom");
```

## Debugging

### Check Applied Styles

In the UI Toolkit Debugger:
1. Select element
2. View "Matched Rules" panel
3. Check which CSS rules are applied
4. Verify cascade priority

### Common Issues

**Styles not applying:**
- Check if USS file is imported correctly
- Verify class names match exactly (case-sensitive)
- Check media query conditions

**Colors not updating:**
- Ensure using CSS variables
- Don't set colors in inline styles
- Apply theme color calculations through CSS

## Resources

- [Unity UI Toolkit Documentation](https://docs.unity3d.com/Manual/UIE-index.html)
- [USS Reference](https://docs.unity3d.com/Manual/UIE-USS-SelectorTypes.html)
- [UXML Documentation](https://docs.unity3d.com/Manual/UIE-UXML.html)
