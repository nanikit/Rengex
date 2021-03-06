﻿using System;
using System.Threading.Tasks;

namespace Rengex.Translator {
  sealed class SelfTranslator : ITranslator {
    private static EztransXp Instance;
    private static Task InitTask;

    public SelfTranslator(int msDelay = 200) {
      if (Instance != null || InitTask != null) {
        return;
      }
      try {
        string cfgEzt = Properties.Settings.Default.EzTransDir;
        InitTask = Task.Run(async () => {
          Instance = await EztransXp.Create(cfgEzt, msDelay).ConfigureAwait(false);
        });
      }
      catch (Exception e) {
        Properties.Settings.Default.EzTransDir = null;
        throw e;
      }
    }

    public async Task<string> Translate(string source) {
      await InitTask.ConfigureAwait(false);
      return await Task.Run(() => Instance.Translate(source)).ConfigureAwait(false);
    }

    public void Dispose() { }
  }
}
