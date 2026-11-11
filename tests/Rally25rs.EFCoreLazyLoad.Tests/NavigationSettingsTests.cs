using Microsoft.Extensions.Logging;
using Rally25rs.EFCoreLazyLoad.Internal;

namespace Rally25rs.EFCoreLazyLoad.Tests;

/// <summary>
///     Unit tests for how <see cref="LazyLoadAttribute" /> settings are resolved and combined with the context's
///     options. No database: these types are never part of an EF model.
/// </summary>
public partial class NavigationSettingsTests
{
    public class Target
    {
    }

    [LazyLoad]
    public partial class Plain
    {
        public partial Target Nav { get; set; }
    }

    [LazyLoad(Mode = LazyLoadMode.Throw, LogLevel = LogLevel.Error)]
    public partial class Base
    {
        public partial Target BaseNav { get; set; }

        [LazyLoad(Mode = LazyLoadMode.Load)]
        public partial Target BaseLoadNav { get; set; }
    }

    [LazyLoad(LogLazyLoads = true)]
    public partial class Derived : Base
    {
        public partial Target DerivedNav { get; set; }
    }

    [LazyLoad(Mode = LazyLoadMode.Load)]
    public partial class DerivedLoad : Base
    {
    }

    public partial class PropertyOnly
    {
        [LazyLoad(LogLazyLoads = false)]
        public partial Target Nav { get; set; }

        [LazyLoad]
        public partial Target PlainNav { get; set; }
    }

    [Fact]
    public void UnsetAttributeProperties_ReturnDefaults()
    {
        var attribute = new LazyLoadAttribute();

        Assert.Equal(LazyLoadMode.Load, attribute.Mode);
        Assert.False(attribute.LogLazyLoads);
        Assert.Equal(LogLevel.Warning, attribute.LogLevel);
        Assert.Null(attribute.ModeOverride);
        Assert.Null(attribute.LogLazyLoadsOverride);
        Assert.Null(attribute.LogLevelOverride);
    }

    [Fact]
    public void SetAttributeProperties_AreOverrides_EvenWhenEqualToDefaults()
    {
        var attribute = new LazyLoadAttribute { Mode = LazyLoadMode.Load, LogLazyLoads = false, LogLevel = LogLevel.Warning };

        Assert.Equal(LazyLoadMode.Load, attribute.ModeOverride);
        Assert.False(attribute.LogLazyLoadsOverride);
        Assert.Equal(LogLevel.Warning, attribute.LogLevelOverride);
    }

    [Fact]
    public void AttributeWithoutSettings_HasNoOverrides()
    {
        Assert.Same(NavigationOverrides.None, NavigationSettings.GetOverrides(typeof(Plain), nameof(Plain.Nav)));
        Assert.Same(NavigationOverrides.None, NavigationSettings.GetOverrides(typeof(PropertyOnly), nameof(PropertyOnly.PlainNav)));
    }

    [Fact]
    public void UnknownNavigationOrUnmarkedType_HasNoOverrides()
    {
        Assert.Same(NavigationOverrides.None, NavigationSettings.GetOverrides(typeof(Plain), "Missing"));
        Assert.Same(NavigationOverrides.None, NavigationSettings.GetOverrides(typeof(Target), "Anything"));
    }

    [Fact]
    public void ClassLevelSettings_ApplyToItsNavigations()
        => Assert.Equal(
            new NavigationOverrides(LazyLoadMode.Throw, null, LogLevel.Error),
            NavigationSettings.GetOverrides(typeof(Base), nameof(Base.BaseNav)));

    [Fact]
    public void PropertyLevelSettings_WinOverClassLevel_PerSetting()
        // Mode comes from the property; LogLevel is still inherited from the class.
        => Assert.Equal(
            new NavigationOverrides(LazyLoadMode.Load, null, LogLevel.Error),
            NavigationSettings.GetOverrides(typeof(Base), nameof(Base.BaseLoadNav)));

    [Fact]
    public void DerivedClass_CombinesItsSettingsWithBaseClassSettings()
    {
        var expected = new NavigationOverrides(LazyLoadMode.Throw, true, LogLevel.Error);

        Assert.Equal(expected, NavigationSettings.GetOverrides(typeof(Derived), nameof(Derived.DerivedNav)));
        Assert.Equal(expected, NavigationSettings.GetOverrides(typeof(Derived), nameof(Base.BaseNav)));
    }

    [Fact]
    public void DerivedClass_OverridesBaseClassSetting()
        => Assert.Equal(
            new NavigationOverrides(LazyLoadMode.Load, null, LogLevel.Error),
            NavigationSettings.GetOverrides(typeof(DerivedLoad), nameof(Base.BaseNav)));

    [Fact]
    public void PropertyLevelSettings_OnInheritedNavigation_StillWin()
        => Assert.Equal(
            LazyLoadMode.Load,
            NavigationSettings.GetOverrides(typeof(Derived), nameof(Base.BaseLoadNav)).Mode);

    [Fact]
    public void PropertyOnlyAttribute_OverridesJustThatSetting()
        => Assert.Equal(
            new NavigationOverrides(null, false, null),
            NavigationSettings.GetOverrides(typeof(PropertyOnly), nameof(PropertyOnly.Nav)));

    [Theory]
    [InlineData(LazyLoadMode.Load, false, LogLevel.Warning)]
    [InlineData(LazyLoadMode.Throw, true, LogLevel.Debug)]
    public void EffectiveSettings_WithoutOverrides_AreTheContextOptions(LazyLoadMode mode, bool log, LogLevel level)
    {
        var options = new LazyLoadOptions { Mode = mode, LogLazyLoads = log, LogLevel = level };

        Assert.Equal(new EffectiveSettings(mode, log, level), EffectiveSettings.From(options, NavigationOverrides.None));
    }

    [Fact]
    public void EffectiveSettings_ApplyOnlyTheOverriddenSettings()
    {
        var options = new LazyLoadOptions { Mode = LazyLoadMode.Load, LogLazyLoads = true, LogLevel = LogLevel.Debug };

        var settings = EffectiveSettings.From(options, new NavigationOverrides(LazyLoadMode.Throw, null, LogLevel.Critical));

        Assert.Equal(new EffectiveSettings(LazyLoadMode.Throw, true, LogLevel.Critical), settings);
    }

    [Theory]
    [InlineData(LazyLoadMode.Load, false, false)]
    [InlineData(LazyLoadMode.Load, true, true)]
    [InlineData(LazyLoadMode.Throw, false, true)]
    public void EffectiveSettings_Intercepts_OnlyWhenThrowingOrLogging(LazyLoadMode mode, bool log, bool expected)
        => Assert.Equal(expected, new EffectiveSettings(mode, log, LogLevel.Warning).Intercepts);
}
