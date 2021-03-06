using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
using RoslynPad.Host;
using RoslynPad.Roslyn.Diagnostics;
using RoslynPad.Runtime;
using RoslynPad.Utilities;
using System.Windows;
using InnovatorAdmin;
using InnovatorAdmin.Connections;
using Innovator.Client;

namespace RoslynPad
{
  internal class MethodViewModel : NotificationObject
  {
    private readonly string _workingDirectory;
    private readonly Dispatcher _dispatcher;

    private ExecutionHost _executionHost;
    private ObservableCollection<ResultObject> _results;
    private CancellationTokenSource _cts;
    private bool _isRunning;
    private Action<object> _executionHostOnDumped;
    private bool _isDirty;
    private Platform _platform;
    private bool _isSaving;
    private IDisposable _viewDisposable;
    private string _configId;
    private Task<IAsyncConnection> _conn;

    public ObservableCollection<ResultObject> Results
    {
      get { return _results; }
      private set { SetProperty(ref _results, value); }
    }

    public DocumentViewModel Document { get; private set; }

    public MethodViewModel(IParentViewModel parentViewModel, DocumentViewModel document, string configId = null)
    {
      _configId = configId;
      _conn = parentViewModel.ConnData.ArasLogin(true).ToTask();
      Document = document;
      ParentViewModel = parentViewModel;
      NuGet = new NuGetDocumentViewModel(parentViewModel.NuGet);
      _dispatcher = Dispatcher.CurrentDispatcher;

      var roslynHost = parentViewModel.RoslynHost;

      IsDirty = document?.IsAutoSave == true;

      _workingDirectory = Document != null
          ? Path.GetDirectoryName(Document.Path)
          : Path.GetTempPath();

      Platform = Platform.X86;
      _executionHost = new ExecutionHost(GetHostExeName(), _workingDirectory,
          roslynHost.DefaultReferences.OfType<PortableExecutableReference>().Select(x => x.FilePath),
          roslynHost.DefaultImports, parentViewModel.NuGetConfiguration, parentViewModel.ChildProcessManager);

      SaveCommand = new DelegateCommand(() => Save(promptSave: false));
      RunCommand = new DelegateCommand(Run, () => !IsRunning);
      RestartHostCommand = new DelegateCommand(RestartHost);
    }

    private string GetHostExeName()
    {
      switch (Platform)
      {
        case Platform.X86:
          return "RoslynPad.Host32.exe";
        case Platform.X64:
          return "RoslynPad.Host64.exe";
        default:
          throw new ArgumentOutOfRangeException(nameof(Platform));
      }
    }

    public Platform Platform
    {
      get { return _platform; }
      set
      {
        if (SetProperty(ref _platform, value))
        {
          if (_executionHost != null)
          {
            _executionHost.HostPath = GetHostExeName();
            RestartHostCommand.Execute();
          }
        }
      }
    }

    private async Task RestartHost()
    {
      Reset();
      await _executionHost.ResetAsync().ConfigureAwait(false);
      SetIsRunning(false);
    }

    private void SetIsRunning(bool value)
    {
      _dispatcher.InvokeAsync(() => IsRunning = value);
    }

    public async Task AutoSave()
    {
      if (!IsDirty) return;
      if (Document == null)
      {
        var index = 1;
        string path;
        do
        {
          path = Path.Combine(_workingDirectory, DocumentViewModel.GetAutoSaveName("Program" + index++));
        } while (File.Exists(path));
        Document = DocumentViewModel.CreateAutoSave(ParentViewModel, path);
      }

      await SaveDocument(Document.IsAutoSave ? Document.Path
          // ReSharper disable once AssignNullToNotNullAttribute
          : Path.Combine(Path.GetDirectoryName(Document.Path), DocumentViewModel.GetAutoSaveName(Document.Name))).ConfigureAwait(false);
    }

    public async Task<SaveResult> Save(bool promptSave)
    {
      if (_isSaving) return SaveResult.Cancel;
      if (!IsDirty) return SaveResult.Save;

      _isSaving = true;
      try
      {
        var result = SaveResult.Save;
        if (Document == null || Document.IsAutoSaveOnly)
        {
          var dialog = new SaveDocumentDialog
          {
            ShowDontSave = promptSave,
            AllowNameEdit = true,
            FilePathFactory = s => DocumentViewModel.GetDocumentPathFromName(_workingDirectory, s)
          };
          dialog.Show();
          result = dialog.Result;
          if (result == SaveResult.Save)
          {
            if (Document?.IsAutoSave == true)
            {
              File.Delete(Document.Path);
            }
            Document = ParentViewModel.AddDocument(dialog.DocumentName);
            OnPropertyChanged(nameof(Title));
          }
        }
        else if (promptSave)
        {
          var dialog = new SaveDocumentDialog
          {
            ShowDontSave = true,
            DocumentName = Document.Name
          };
          dialog.Show();
          result = dialog.Result;
        }
        if (result == SaveResult.Save)
        {
          // ReSharper disable once PossibleNullReferenceException
          await SaveDocument(Document.Path).ConfigureAwait(true);
          IsDirty = false;
        }
        return result;
      }
      finally
      {
        _isSaving = false;
      }
    }

    private async Task SaveDocument(string path)
    {
      if (DocumentId == null) return;

      var text = await ParentViewModel.RoslynHost.GetDocument(DocumentId).GetTextAsync().ConfigureAwait(false);
      using (var writer = new StreamWriter(path, append: false))
      {
        for (int lineIndex = 0; lineIndex < text.Lines.Count - 1; ++lineIndex)
        {
          var lineText = text.Lines[lineIndex].ToString();
          await writer.WriteLineAsync(lineText).ConfigureAwait(false);
        }
        await writer.WriteAsync(text.Lines[text.Lines.Count - 1].ToString()).ConfigureAwait(false);
      }
    }

    public async Task Initialize(SourceTextContainer sourceTextContainer, Action<DiagnosticsUpdatedArgs> onDiagnosticsUpdated, Action<SourceText> onTextUpdated, IDisposable viewDisposable)
    {
      _viewDisposable = viewDisposable;
      var roslynHost = ParentViewModel.RoslynHost;
      // ReSharper disable once AssignNullToNotNullAttribute
      if (Document == null)
      {
        var code = GetMethodCode(null, ParentViewModel.ConnData);
        onTextUpdated(SourceText.From(code));
      }
      DocumentId = roslynHost.AddDocument(sourceTextContainer, _workingDirectory, onDiagnosticsUpdated, onTextUpdated);
      await _executionHost.ResetAsync().ConfigureAwait(false);
    }

    private string GetMethodCode(string code, ConnectionData connData)
    {
      using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/RoslynPad;component/Resources/DefaultMethodCode.csx")).Stream)
      using (var reader = new StreamReader(stream))
      {
        var template = reader.ReadToEnd();
        template = template.Replace("/*****``URL_PARAMS``*****/", string.Format(@"var __param_url = ""{0}"";
var __param_db = ""{1}"";
var __param_user = ""{2}"";
var __param_pass = ""{3}"";"
          , ParentViewModel.ConnData.Url
          , ParentViewModel.ConnData.Database
          , ParentViewModel.ConnData.UserName
          , InnovatorAdmin.ConnectionDataExtensions.CalcMD5(ParentViewModel.ConnData.Password)))
        .Replace("/*****``METHOD_CODE``*****/", code);
        return template;
      }
    }

    public DocumentId DocumentId { get; private set; }

    public IParentViewModel ParentViewModel { get; }

    public NuGetDocumentViewModel NuGet { get; }

    public string Title => Document != null && !Document.IsAutoSaveOnly ? Document.Name : "New";

    public DelegateCommand SaveCommand { get; }

    public DelegateCommand RunCommand { get; }

    public DelegateCommand RestartHostCommand { get; }

    public bool IsRunning
    {
      get { return _isRunning; }
      private set
      {
        if (SetProperty(ref _isRunning, value))
        {
          _dispatcher.InvokeAsync(() => RunCommand.RaiseCanExecuteChanged());
        }
      }
    }

    private async Task Run()
    {
      if (IsRunning) return;

      Reset();

      await ParentViewModel.AutoSaveOpenDocuments().ConfigureAwait(false);

      SetIsRunning(true);

      var results = new ObservableCollection<ResultObject>();
      Results = results;

      var cancellationToken = _cts.Token;
      if (_executionHostOnDumped != null)
      {
        _executionHost.Dumped -= _executionHostOnDumped;
      }
      _executionHostOnDumped = o => AddResult(o, results, cancellationToken);
      _executionHost.Dumped += _executionHostOnDumped;
      try
      {
        var code =
            await
                ParentViewModel.RoslynHost.GetDocument(DocumentId)
                    .GetTextAsync(cancellationToken)
                    .ConfigureAwait(true);
        await _executionHost.ExecuteAsync(code.ToString()).ConfigureAwait(true);
      }
      catch (CompilationErrorException ex)
      {
        foreach (var diagnostic in ex.Diagnostics)
        {
          results.Add(ResultObject.Create(diagnostic));
        }
      }
      catch (Exception ex)
      {
        AddResult(ex, results, cancellationToken);
      }
      finally
      {
        SetIsRunning(false);
      }
    }

    private void Reset()
    {
      if (_cts != null)
      {
        _cts.Cancel();
        _cts.Dispose();
      }
      _cts = new CancellationTokenSource();
    }

    private void AddResult(object o, ObservableCollection<ResultObject> results, CancellationToken cancellationToken)
    {
      _dispatcher.InvokeAsync(() =>
      {
        var list = o as IList<ResultObject>;
        if (list != null)
        {
          foreach (var resultObject in list)
          {
            results.Add(resultObject);
          }
        }
        else
        {
          results.Add(ResultObject.Create(o));
        }
      }, DispatcherPriority.SystemIdle, cancellationToken);
    }

    public async Task<string> LoadText()
    {
      if (string.IsNullOrEmpty(_configId))
      {
        return string.Empty;
      }
      var conn = await _conn;
      var result = await conn.ApplyAsync("<Item type='Method' action='get'><config_id>@0</config_id></Item>", true, true, _configId).ToTask();
      return GetMethodCode(result.AssertItem().Property("method_code").Value, ParentViewModel.ConnData);
    }

    public void Close()
    {
      _viewDisposable?.Dispose();
      _executionHost?.Dispose();
      _executionHost = null;
    }

    public bool IsDirty
    {
      get { return _isDirty; }
      private set { SetProperty(ref _isDirty, value); }
    }

    public void SetDirty(int textLength)
    {
      IsDirty = textLength > 0;
    }
  }
}
