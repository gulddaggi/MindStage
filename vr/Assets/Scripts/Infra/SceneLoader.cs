using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace App.Infra
{
    /// <summary>씬 전환을 담당하는 유틸. (추후 로딩UI/예외처리/Addressables 확장 지점)</summary>

    public static class SceneLoader
    {
        public static async Task LoadSingleAsync(string scenePath)
        {
            var op = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            while (!op.isDone) await System.Threading.Tasks.Task.Yield();
        }
    }
}
