﻿using Bonsai.Design;
using Bonsai.Editor.GraphModel;
using Bonsai.Expressions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace Bonsai.Editor
{
    public class WorkflowRunner
    {
        static void RunLayout(
            WorkflowBuilder workflowBuilder,
            Dictionary<string, string> propertyAssignments,
            IObservable<TypeVisualizerDescriptor> visualizerProvider,
            VisualizerLayout layout,
            string fileName)
        {
            var typeVisualizers = new TypeVisualizerMap();
            var loadVisualizers = (from typeVisualizer in visualizerProvider
                                   let targetType = Type.GetType(typeVisualizer.TargetTypeName)
                                   let visualizerType = Type.GetType(typeVisualizer.VisualizerTypeName)
                                   where targetType != null && visualizerType != null
                                   select (targetType, visualizerType))
                                   .Do(entry => typeVisualizers.Add(entry.targetType, entry.visualizerType))
                                   .ToEnumerable().ToList();

            workflowBuilder = new WorkflowBuilder(workflowBuilder.Workflow.ToInspectableGraph());
            BuildAssignProperties(workflowBuilder, propertyAssignments);
            LayoutHelper.SetWorkflowNotifications(workflowBuilder.Workflow, publishNotifications: false);
            LayoutHelper.SetLayoutTags(workflowBuilder.Workflow, layout);
            LayoutHelper.SetLayoutNotifications(layout);

            var services = new System.ComponentModel.Design.ServiceContainer();
            services.AddService(typeof(WorkflowBuilder), workflowBuilder);
            var runtimeWorkflow = workflowBuilder.Workflow.BuildObservable();

            var cts = new CancellationTokenSource();
            var contextMenu = new ContextMenuStrip();
            void CreateVisualizerMapping(ExpressionBuilderGraph workflow, VisualizerLayout layout)
            {
                var mapping = LayoutHelper.CreateVisualizerMapping(workflow, layout, typeVisualizers, services);
                foreach (var launcher in mapping.Values.Where(launcher => launcher.Visualizer.IsValueCreated))
                {
                    var activeLauncher = launcher;
                    contextMenu.Items.Add(new ToolStripMenuItem(launcher.Text, null, (sender, e) =>
                    {
                        activeLauncher.Show(services);
                    }));
                }

                foreach (var settings in layout.DialogSettings)
                {
                    if (settings is WorkflowEditorSettings editorSettings &&
                        editorSettings.Tag is ExpressionBuilder builder &&
                        ExpressionBuilder.Unwrap(builder) is IWorkflowExpressionBuilder workflowBuilder &&
                        editorSettings.EditorVisualizerLayout != null &&
                        editorSettings.EditorDialogSettings.Visible)
                    {
                        CreateVisualizerMapping(workflowBuilder.Workflow, editorSettings.EditorVisualizerLayout);
                    }
                }
            }
            CreateVisualizerMapping(workflowBuilder.Workflow, layout);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(new ToolStripMenuItem("Stop", null, (sender, e) => cts.Cancel()));

            using var notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Properties.Resources.Icon;
            notifyIcon.Text = Path.GetFileName(fileName);
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.Visible = true;

            var synchronizationContext = new WindowsFormsSynchronizationContext();
            runtimeWorkflow.Finally(() =>
            {
                // Posting the exit to the main thread's winforms sync context is important for two reasons:
                // 1) If this finally action will be executed on the main thread (IE: the workflow is empty) we need to
                //    defer Application.Exit until Application.Run, otherwise we're trying to exit a message loop which
                //    has not even started.
                // 2) When this finally action executes it will be on a background thread. While Application.Exit can
                //    be called from any thread, many FormClosed callbacks within Bonsai assume they're running on the
                //    main thread. Application.Exit invokes the FormClose (and FormClosing) callbacks directly.
                //
                // As an added bonus this also ensures any exceptions thrown via Application.Exit (especially those
                // within FormClose/FormClosing handlers) will be observed as exceptions thrown in a finally action
                // are silently swallowed.
                synchronizationContext.Post(_ => Application.Exit(), null);
            }).Subscribe(
                unit => { },
                ex => { Console.Error.WriteLine(ex); },
                () => { },
                cts.Token);

            Application.Run();
        }

        static void RunHeadless(WorkflowBuilder workflowBuilder, Dictionary<string, string> propertyAssignments)
        {
            BuildAssignProperties(workflowBuilder, propertyAssignments);
            var workflowCompleted = new ManualResetEvent(false);
            workflowBuilder.Workflow.BuildObservable().Subscribe(
                unit => { },
                ex => { Console.Error.WriteLine(ex); workflowCompleted.Set(); },
                () => workflowCompleted.Set());
            workflowCompleted.WaitOne();
        }

        static void BuildAssignProperties(WorkflowBuilder workflowBuilder, Dictionary<string, string> propertyAssignments)
        {
            workflowBuilder.Workflow.Build();
            foreach (var assignment in propertyAssignments)
            {
                workflowBuilder.Workflow.SetWorkflowProperty(assignment.Key, assignment.Value);
            }
        }

        public static void Run(string fileName, Dictionary<string, string> propertyAssignments, IObservable<TypeVisualizerDescriptor> visualizerProvider = null)
        {
            Run(fileName, propertyAssignments, visualizerProvider);
        }

        public static void Run(
            string fileName,
            Dictionary<string, string> propertyAssignments,
            IObservable<TypeVisualizerDescriptor> visualizerProvider = null,
            string layoutPath = null)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (!File.Exists(fileName))
            {
                throw new ArgumentException("Specified workflow file does not exist.", nameof(fileName));
            }

            var workflowBuilder = ElementStore.LoadWorkflow(fileName);
            layoutPath ??= LayoutHelper.GetLayoutPath(fileName);
            if (visualizerProvider != null && File.Exists(layoutPath))
            {
                VisualizerLayout layout = null;
                using (var reader = XmlReader.Create(layoutPath))
                {
                    layout = (VisualizerLayout)VisualizerLayout.Serializer.Deserialize(reader);
                }

                RunLayout(workflowBuilder, propertyAssignments, visualizerProvider, layout, fileName);
            }
            else RunHeadless(workflowBuilder, propertyAssignments);
        }
    }
}
