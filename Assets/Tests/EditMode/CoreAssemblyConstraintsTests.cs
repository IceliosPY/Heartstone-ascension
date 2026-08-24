using System;
using System.Linq;
using System.Reflection;
using CoH.Core;
using NUnit.Framework;

namespace CoH.Tests.EditMode
{
    /// <summary>
    /// Architecture tests (Phase 0).
    ///
    /// These tests verify no game rule. They lock down the project's
    /// structural constraint: CoH.Core stays a pure C# library, compilable
    /// outside Unity and testable without loading a scene.
    ///
    /// They also act as a guard rail: if anyone ever turns off
    /// noEngineReferences in CoH.Core.asmdef, this test will fail.
    /// </summary>
    public sealed class CoreAssemblyConstraintsTests
    {
        private static Assembly Core => typeof(CoreAssembly).Assembly;

        [Test]
        public void Core_assembly_is_loaded_and_correctly_named()
        {
            Assert.That(Core.GetName().Name, Is.EqualTo(CoreAssembly.Name));
        }

        [Test]
        public void Core_assembly_does_not_reference_Unity()
        {
            string[] unityReferences = Core
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .Where(name => name.StartsWith("UnityEngine", StringComparison.Ordinal)
                            || name.StartsWith("UnityEditor", StringComparison.Ordinal)
                            || name.StartsWith("Unity.", StringComparison.Ordinal))
                .ToArray();

            Assert.That(
                unityReferences,
                Is.Empty,
                "CoH.Core must stay pure C# (rules engine decoupled from Unity). "
                + "Unity references found: " + string.Join(", ", unityReferences));
        }
    }
}
