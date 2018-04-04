﻿/*
*プログラム: HUD_MessageManager
*   最終更新日:
*       11.11.2016
*
*   説明:
*       UI
*   
*   更新履歴:
*       3.4.2016:
*           プログラムの完成
*
*       3.16.2016:
*           サポート関数を追加
*
*       3.28.2016:
*           MESSAGE_TYPEにHIGHER, LOWERを追加
*
*       3.31.2016:
*           スクリプト修正
*
*       4.2.1016:
*           MESSAGE_TYPEにUP_LEFT, DOWN_RIGHTを追加
*           HIGHRTをUPに変更, LOWERをDOWNに変更
*
*       4.18.2016:
*           TimeScaleの影響を受けないように仕様を変更
*
*       7.9.2016:
*           シーンが読まれるときにmessageを削除するようにした
*               使われなくなったmessageを削除するため
*               ただし, dontDeleteOnLoadがtrueのmessageは削除されない
*
*       7.10.2016:
*           名前の重複を防ぐため列挙型パラメータをHUD_MessageManager内にカプセル化
*           ID_TYPE―IDの有効範囲の設定―を追加
*
*       8.2.2016:
*           シーン間でデストロイされなくした
*           表示にかける時間と文字を消すのにかける時間を設定可能にした
*           設定関数を追加
*           表示関数を追加
*           
*       11.11.2016:
*           UnityUpdateに伴う修正; OnLevelWasLoadedの代わりにSceneManagerを使用
*           
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
//using TMPro;


public class HUD_MessageManager : MonoBehaviour
{
    public static HUD_MessageManager Instance { get; private set; }

    //===外部パラメータ(Inspector表示)===================================================
    public float defaultEntranceTime = 1.0f;
    public float defaultDisplayTime = 3.0f;
    public float defaultExitTime = 1.0f;

    public GameObject textBoxObject;
    public GameObject centerTextObject;
    public GameObject topTextObject;
    public GameObject bottomTextObject;
    public GameObject topLeftTextObject;
    public GameObject bottomRightTextObject;
    public GameObject textBoxTextObject;

    //===外部パラメータ==================================================================

    //列挙: public enum MESSAGE_TYPE
    //  説明:
    //      メッセージ描画系
    //      メッセージの描画位置を指定するときに使います
    //  
    //  リスト:
    //      BOX:
    //          TextBox内
    //
    //      CENTER:
    //          画面中央
    public enum MessageType
    {
        Box,
        Center,
        Top,
        Bottom,
        TopLeft,
        BottomRight
    }

    //列挙: public enum MESSAGE_ENTRANCE
    //  説明:
    //      メッセージ処理系
    //      メッセージ開始アニメーションを指定するときに使います
    //      
    //  リスト:
    //      APPEAR:
    //          速やかに表示
    //
    //      FADE:
    //          徐々に表示
    public enum MessageEntrance
    {
        Appear,
        Fade
    }

    //列挙: public enum MESSAGE_EXIT
    //  説明:
    //      メッセージ処理系
    //      メッセージ終了アニメーションを指定するときに使います
    //      
    //  リスト:
    //      APPEAR:
    //          速やかに消す
    //
    //      FADE:
    //          徐々に消す
    public enum MessageExit
    {
        Disappear,
        Fade
    }

    //列挙: public enum MESSAGE_MODE
    //  説明:
    //      メッセージ処理系
    //      メッセージのモードを指定するときに使います
    //
    //  リスト:
    //      NORMAL:
    //          通常モード
    //
    //      TIMER:
    //          タイマーモード
    //          指定した時間がたつとメッセージを自動で消します
    public enum MessageMode
    {
        Normal,
        Timer
    }

    public enum IdType
    {
        Normal,
        keepIdButDeleteOnLoad,
        DontDeleteOnLoad
    }

    //クラス: public class Param
    //  説明:
    //      メッセージに関する設定
    //
    //  メンバー変数:
    //      public MESSAGE_ENTRANCE entranceAnimation:
    //          ここではメッセージ開始アニメーションを設定します
    //
    //      public MESSAGE_EXIT exitAnimation:
    //          ここではメッセージ終了アニメーションを設定します
    //
    //      public float displayTime:
    //          ここでは表示時間を設定します
    //          タイマーモードで使用します
    //
    //      public transparencyRate:
    //          ここでは透明度の変化の割合を設定します
    //          アニメーション'FADE'で使用します
    //
    //      public string color:
    //          ここでは文字色を設定します
    //          透明度は含みません
    //
    //  メンバ関数:
    //      public object Clone():
    //          このクラスを複製したオブジェクトを返します
    public class Param
    {
        public MessageEntrance entranceAnimation = MessageEntrance.Appear;
        public MessageExit exitAnimation = MessageExit.Disappear;
        public float displayTime = 2.0f;
        public float exitTime = 1.0f;
        public float entranceTime = 1.0f;
        public string color = "000000";
        public IdType idType = IdType.Normal;

        public object Clone()
        {
            Param work = new Param();

            work.entranceAnimation = this.entranceAnimation;
            work.exitAnimation = this.exitAnimation;
            work.displayTime = this.displayTime;
            work.exitTime = this.exitTime;
            work.entranceTime = this.entranceTime;
            work.color = this.color;
            work.idType = this.idType;
            return work;
        }

    }

    // ===内部パラメータ====================================================================
    private class ParamPrivate
    {
        public bool activated = false;
        public bool isPlaying = false;
        public MessageState messageState = MessageState.Ready;
        public string text;
        public float transparency = 255.0f;
        public float messageStartTime;
        public MessageMode messageMode = MessageMode.Normal;
        public MessageType messageType = MessageType.Center;
    }

    //各messageElementが持つ変数群
    private class MessageListElement
    {
        public ParamPrivate paramPrivate;
        public Param param;
    }

    //列挙: private enum MESSAGE_STATE
    //  説明:
    //      各メッセージの状態
    //
    //  リスト:
    //      READY:
    //          STARTに入る前
    //
    //      START:
    //          メッセージ開始
    //
    //      RUNNING:
    //          メッセージ表示中
    //
    //      TO_END:
    //          メッセージ終了中
    //
    //      END:
    //          メッセージ終了
    private enum MessageState
    {
        Ready,
        Start,
        Showing,
        ToEnd,
        End
    }

    //各メッセージを格納したリスト
    private List<MessageListElement> messageList = new List<MessageListElement>();

    //===キャッシュ=============================================================================
    
    Text centerTextText;
    Text textBoxTextText;
    Text topTextText;
    Text bottomTextText;
    Text topLeftTextText;
    Text bottomRightTextText;

    //===コード==================================================================================
    void OnScenelWasLoaded(Scene scenename, LoadSceneMode SceneMode)
    {
        //シーンが読まれるときdontDeleteOnLoadがfalseのmessageは破棄
        foreach (var message in messageList)
        {
            if (message.param.idType != IdType.DontDeleteOnLoad)
            {
                message.paramPrivate.activated = false;
            }
        }

    }

    void Awake()
    {
        //各オブジェクト取得
        //Transform trfm = transform.Find("CenterText");

        if (centerTextObject != null && (centerTextText = centerTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> CenterTextに関する情報を取得できません. これに関する機能は使用できません.");

        }

        if (textBoxObject != null && textBoxTextObject != null && (textBoxTextText = textBoxTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> TextBoxに関する情報を取得できません. これに関する機能は使用できません.");
        }

        if (topTextObject != null && (topTextText = topTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> TopTextに関する情報を取得できません. これに関する機能は使用できません.");
        }
        
        if (bottomTextObject != null && (bottomTextText = bottomTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> BottomTextに関する情報を取得できません. これに関する機能は使用できません.");
        }
        
        if (topLeftTextObject != null && (topLeftTextText = topLeftTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> TopLeftTextに関する情報を取得できません. これに関する機能は使用できません.");
        }
        
        if (bottomRightTextObject != null && (bottomRightTextText = bottomRightTextObject.GetComponent<Text>()) != null)
        {
        }
        else
        {
            Debug.LogWarning("HUD_MessageManager.Awake >> BottomRightTextに関する情報を取得できません. これに関する機能は使用できません.");
        }

        //TextBox非表示
        textBoxObject.SetActive(false);

        InvokeRepeating("RearrangeMessageList", 0.0f, 5.0f);

        SceneManager.sceneLoaded += OnScenelWasLoaded;
        DontDestroyOnLoad(this);
        Instance = this;
    }

    //メッセージ処理
    void Update()
    {
        //メッセージ状態処理
        UpdateMessage();

        //メッセージ描画処理
        WriteMessage();
    }

    //関数: private void UpdateMessage()
    //  説明:
    //      メッセージの処理を行います
    //      描画以外のことを行います
    //      各メッセージの状態による処理を行います
    private void UpdateMessage()
    {
        int i;
        Param param;
        ParamPrivate paramPrivate;

        //各メッセージを処理
        for (i = 0; i < messageList.Count; i++)
        {

            paramPrivate = messageList[i].paramPrivate;
            param = messageList[i].param;

            //メッセージが有効になっているとき
            if (paramPrivate.activated)
            {
                switch (paramPrivate.messageState)
                {
                    //READY状態
                    case MessageState.Ready:
                        break;

                    //START状態
                    case MessageState.Start:
                        switch (param.entranceAnimation)
                        {
                            case MessageEntrance.Appear:
                                paramPrivate.transparency = 255.0f;
                                paramPrivate.messageState = MessageState.Showing;
                                break;

                            case MessageEntrance.Fade:
                                if (param.entranceTime > 0)
                                {
                                    paramPrivate.transparency += (255.0f / param.entranceTime) * Time.unscaledDeltaTime;
                                }
                                else
                                {
                                    paramPrivate.transparency = 255.0f;
                                }

                                if (paramPrivate.transparency >= 255.0f)
                                {
                                    paramPrivate.transparency = 255.0f;
                                    paramPrivate.messageState = MessageState.Showing;
                                }
                                break;
                        }
                        break;

                    //TO_END状態
                    case MessageState.ToEnd:
                        switch (param.exitAnimation)
                        {
                            case MessageExit.Disappear:
                                paramPrivate.transparency = 0.0f;
                                paramPrivate.messageState = MessageState.End;
                                paramPrivate.isPlaying = false;
                                break;

                            case MessageExit.Fade:
                                if (param.exitTime > 0)
                                {
                                    paramPrivate.transparency -= (255.0f / param.exitTime) * Time.unscaledDeltaTime;
                                }
                                else
                                {
                                    paramPrivate.transparency = 0.0f;
                                }

                                if (paramPrivate.transparency <= 0.0f)
                                {
                                    paramPrivate.transparency = 0.0f;
                                    paramPrivate.messageState = MessageState.End;
                                    paramPrivate.isPlaying = false;
                                }
                                break;
                        }
                        break;

                    //SHOWNING状態
                    case MessageState.Showing:
                        switch (paramPrivate.messageMode)
                        {
                            case MessageMode.Normal:
                                break;

                            case MessageMode.Timer:
                                if (Time.unscaledTime - paramPrivate.messageStartTime > param.displayTime)
                                {
                                    Exit(i);
                                }
                                break;
                        }
                        break;

                    //END状態
                    case MessageState.End:
                        if (param.idType == IdType.Normal)
                        {
                            paramPrivate.activated = false;
                        }
                        else
                        {
                            paramPrivate.activated = true;
                        }

                        paramPrivate.messageState = MessageState.Ready;
                        break;
                }
            }
        }
    }

    //関数: private WriteMessage()
    //  説明:
    //      メッセージの描画処理を行います
    //      コンポーネント"Text"内のメンバー変数"Text"に文字列を格納します
    private void WriteMessage()
    {
        bool textBoxSwitch = false;

        string centerText = "";
        string textBoxText = "";
        string topText = "";
        string bottomText = "";
        string topLeftText = "";
        string bottomRightText = "";

        //各メッセージを処理
        for (int i = 0; i < messageList.Count; i++)
        {
            string text = "";

            ParamPrivate paramPrivate = messageList[i].paramPrivate;
            Param param = messageList[i].param;

            //メッセージ内容代入
            if (paramPrivate.activated)
            {
                if (messageList[i].paramPrivate.isPlaying)
                {
                    int transparencyInt = (int)paramPrivate.transparency;
                    text += "<color=#" + param.color + "{a}" + ">";
                    text += paramPrivate.text;
                    text += "</color>\n";
                    text = text.Replace("{a}", transparencyInt.ToString("X2"));

                    if (paramPrivate.messageType == MessageType.Box)
                    {
                        textBoxSwitch = true;
                    }
                }
            }

            //各描画位置へメッセージを代入
            switch (paramPrivate.messageType)
            {
                case MessageType.Center:
                    centerText += text;
                    break;

                case MessageType.Box:
                    textBoxText += text;
                    break;

                case MessageType.Top:
                    topText += text;
                    break;

                case MessageType.Bottom:
                    bottomText += text;
                    break;

                case MessageType.TopLeft:
                    topLeftText += text;
                    break;

                case MessageType.BottomRight:
                    bottomRightText += text;
                    break;
            }
        }

        //テキストボックス表示,非表示設定
        if (textBoxSwitch ^ textBoxObject.activeSelf)
        {
            textBoxObject.SetActive(textBoxSwitch);
        }

        //コンポーネント"Text"に文字列を格納
        centerTextText.text = centerText;
        textBoxTextText.text = textBoxText;
        topTextText.text = topText;
        bottomTextText.text = bottomText;
        topLeftTextText.text = topLeftText;
        bottomRightTextText.text = bottomRightText;
    }

    //関数: private void InitMessageList()
    //  説明:
    //      messageListを初期化します
    private void InitMessageList()
    {
        int i;
        for (i = 0; i < messageList.Count; i++)
        {
            messageList[i] = new MessageListElement();

            CreateMessageElement(i, new ParamPrivate(), new Param());
        }
    }

    //関数: private int CreateMessageElement(int index, ParamPrivate paramPrivate, Param param)
    //  説明:
    //      messageList内の指定された場所にmessageElementを作成します
    private int CreateMessageElement(int index, ParamPrivate paramPrivate, Param param)
    {
        //格納数が足らないとき新たに格納場所を追加
        if (messageList.Count < index + 1)
        {
            messageList.Add(new MessageListElement());
        }

        //メッセージ変数を代入
        messageList[index].paramPrivate = paramPrivate;
        messageList[index].param = param;

        return index;
    }

    //messageListを整理します
    private void RearrangeMessageList()
    {
        int startIndexToRemove;

        //削除を始める場所
        startIndexToRemove = -1;

        //最後にactivatedがfalseになっている場所を検索
        for (int i = 0; i < messageList.Count; i++)
        {
            ParamPrivate paramPrivate = messageList[i].paramPrivate;
            //Debug.Log(string.Format("[HUD_MessageManager] ID{0}.activated: {1}", i, paramPrivate.activated));
            //Debug.Log(paramPrivate.text);

            if (paramPrivate.activated)
            {
                startIndexToRemove = i + 1;
            }
            else
            {
                startIndexToRemove = i;
            }
        }

        if (startIndexToRemove >= 0)
        {
            messageList.RemoveRange(startIndexToRemove, messageList.Count - startIndexToRemove);
        }

        //現在使用している格納数に2つの余裕を持たせるように容量を確保
        messageList.Capacity = messageList.Count + 2;

        //Debug.Log(messageList.Count);
    }

    //
    //関数: 
    //  説明:
    //      messageを設定する
    //
    /// <summary>
    /// messageを設定する
    /// </summary>
    /// <param name="text"></param>
    /// <param name="type"></param>
    /// <param name="mode"></param>
    /// <param name="messageParam"></param>
    /// <returns></returns>
    public int Set(string text, MessageType type, MessageMode mode, Param messageParam)
    {
        int i;
        ParamPrivate paramPrivate;

        paramPrivate = new ParamPrivate();

        paramPrivate.activated = true;
        paramPrivate.isPlaying = false;
        paramPrivate.messageState = MessageState.Ready;
        paramPrivate.text = text;
        paramPrivate.messageStartTime = 0.0f;
        paramPrivate.messageMode = mode;
        paramPrivate.messageType = type;
        paramPrivate.transparency = 0.0f;

        if (messageList.Count == 0)
        {
            return CreateMessageElement(0, paramPrivate, (Param)messageParam.Clone());
        }
        for (i = 0; i < messageList.Count; i++)
        {
            if (!messageList[i].paramPrivate.activated)
            {
                break;
            }
        }

        return CreateMessageElement(i, paramPrivate, (Param)messageParam.Clone());
    }

    //
    //関数: 
    //  説明:
    //      メッセージを設定します
    //      そのメッセージはCenterTextに表示されます
    //      Alert関係メッセージ設定の基本関数です
    //
    public int SetAlert(string text, float entranceTime, float displayTime, float exitTime, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Fade;
        param.exitAnimation = MessageExit.Fade;
        param.entranceTime = entranceTime;
        param.displayTime = displayTime;
        param.exitTime = exitTime;
        param.idType = idType;
        return Set(text, MessageType.Center, MessageMode.Timer, param);
    }

    //文字と時間を引数にとる
    /// <summary>
    /// 画面中央のMessageを設定します. このMessageが終了すると同時に, このMessageは削除されます.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <returns></returns>
    public int SetAlert(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetAlert(text, entranceTime, displayTime, exitTime, IdType.Normal);
    }

    /// <summary>
    /// 画面中央のMessageを設定します. このMessageが終了すると同時に, このMessageは削除されます.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="displayTime"></param>
    /// <returns></returns>
    public int SetAlert(string text, float displayTime)
    {
        return SetAlert(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    /// <summary>
    /// 画面中央のMessageを設定します. 
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <param name="dontDeleteOnLoad"></param>
    /// <returns></returns>
    public int SetAlertKeepID(string text, float entranceTime, float displayTime, float exitTime, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetAlert(text, entranceTime, displayTime, exitTime, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetAlert(text, entranceTime, displayTime, exitTime, IdType.keepIdButDeleteOnLoad);
        }
    }

    /// <summary>
    /// 画面中央のMessageを設定します
    /// </summary>
    /// <param name="text"></param>
    /// <param name="displayTime"></param>
    /// <param name="dontDeleteOnLoad"></param>
    /// <returns></returns>
    public int SetAlertKeepID(string text, float displayTime, bool dontDeleteOnLoad)
    {
        return SetAlertKeepID(text, defaultEntranceTime, displayTime, defaultExitTime, dontDeleteOnLoad);
    }

    /// <summary>
    /// 画面中央のMessageを設定します
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <returns></returns>
    public int SetAlertKeepID(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetAlertKeepID(text, entranceTime, displayTime, exitTime, false);
    }

    /// <summary>
    /// 画面中央のMessageを設定します
    /// </summary>
    /// <param name="text"></param>
    /// <param name="displayTime"></param>
    /// <returns></returns>
    public int SetAlertKeepID(string text, float displayTime)
    {
        return SetAlertKeepID(text, defaultEntranceTime, displayTime, defaultExitTime);
    }


    //文字のみを引数にとる
    public int SetAlert(string text)
    {
        return SetAlert(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime);
    }

    public int SetAlertKeepID(string text, bool dontDeleteOnLoad)
    {
        return SetAlertKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetAlertKeepID(string text)
    {
        return SetAlertKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, false);
    }

    /// <summary>
    /// メッセージを設定します
    ///      そのメッセージはTopTextに表示されます
    ///      SubtitleTop関係メッセージ設定の基本関数です
    /// </summary>
    /// <param name="text"></param>
    /// <param name="idType"></param>
    /// <returns></returns>
    public int SetSubtitleTop(string text, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Appear;
        param.exitAnimation = MessageExit.Disappear;
        param.idType = idType;
        return Set(text, MessageType.Top, MessageMode.Normal, param);
    }

    //文字のみを引数にとる
    public int SetSubtitleTop(string text)
    {
        return SetSubtitleTop(text, IdType.Normal);
    }

    public int SetSubtitleTopKeepID(string text, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetSubtitleTop(text, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetSubtitleTop(text, IdType.keepIdButDeleteOnLoad);
        }
    }

    public int SetSubtitleTopKeepID(string text)
    {
        return SetSubtitleTopKeepID(text, false);
    }

    /// <summary>
    /// 
    ///      メッセージを設定します
    ///      そのメッセージはTopTextに表示されます
    ///      TimerModeで動作します
    ///      SubtitleTopTimer関係メッセージ設定の基本関数です
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <param name="idType"></param>
    /// <returns></returns>
    public int SetSubtitleTopTimer(string text, float entranceTime, float displayTime, float exitTime, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Appear;
        param.exitAnimation = MessageExit.Disappear;
        param.entranceTime = entranceTime;
        param.displayTime = displayTime;
        param.exitTime = exitTime;
        param.idType = idType;
        return Set(text, MessageType.Top, MessageMode.Timer, param);
    }

    //文字と時間を引数にとる
    public int SetSubtitleTopTimer(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetSubtitleTopTimer(text, entranceTime, displayTime, exitTime, IdType.Normal);
    }
    public int SetSubtitleTopTimer(string text, float displayTime)
    {
        return SetSubtitleTopTimer(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    public int SetSubtitleTopTimerKeepID(string text, float entranceTime, float displayTime, float exitTime, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetSubtitleTopTimer(text, entranceTime, displayTime, exitTime, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetSubtitleTopTimer(text, entranceTime, displayTime, exitTime, IdType.keepIdButDeleteOnLoad);
        }
    }
    public int SetSubtitleTopTimerKeepID(string text, float displayTime, bool dontDeleteOnLoad)
    {
        return SetSubtitleTopTimerKeepID(text, defaultEntranceTime, displayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetSubtitleTopTimerKeepID(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetSubtitleTopTimerKeepID(text, entranceTime, displayTime, exitTime, false);
    }
    public int SetSubtitleTopTimerKeepID(string text, float displayTime)
    {
        return SetSubtitleTopTimerKeepID(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    //文字のみを引数にとる
    public int SetSubtitleTopTimer(string text)
    {
        return SetSubtitleTopTimer(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime);
    }

    public int SetSubtitleTopTimerKeepID(string text, bool dontDeleteOnLoad)
    {
        return SetSubtitleTopTimerKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetSubtitleTopTimerKeepID(string text)
    {
        return SetSubtitleTopTimerKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, false);
    }

    //
    //関数:
    //  説明:
    //      メッセージを設定します
    //      そのメッセージはBottomTextに表示されます
    //      SubtitleBottom関係メッセージ設定の基本関数です
    //
    public int SetSubtitleBottom(string text, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Appear;
        param.exitAnimation = MessageExit.Disappear;
        param.idType = idType;
        return Set(text, MessageType.Bottom, MessageMode.Normal, param);
    }

    //文字のみを引数にとる
    public int SetSubtitleBottom(string text)
    {
        return SetSubtitleBottom(text, IdType.Normal);
    }

    public int SetSubtitleBottomKeepID(string text, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetSubtitleBottom(text, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetSubtitleBottom(text, IdType.keepIdButDeleteOnLoad);
        }
    }

    public int SetSubtitleBottomKeepID(string text)
    {
        return SetSubtitleBottomKeepID(text, false);
    }

    //
    //関数:
    //  説明:
    //      メッセージを設定します
    //      そのメッセージはBottomTextに表示されます
    //      TimerModeで動作します
    //      SubtitleBottomTimer関係メッセージ設定の基本関数です
    //
    public int SetSubtitleBottomTimer(string text, float entranceTime, float displayTime, float exitTime, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Appear;
        param.exitAnimation = MessageExit.Disappear;
        param.entranceTime = entranceTime;
        param.displayTime = displayTime;
        param.exitTime = exitTime;
        param.idType = idType;
        return Set(text, MessageType.Bottom, MessageMode.Timer, param);
    }

    //文字と時間を引数にとる
    public int SetSubtitleBottomTimer(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetSubtitleBottomTimer(text, entranceTime, displayTime, exitTime, IdType.Normal);
    }
    public int SetSubtitleBottomTimer(string text, float displayTime)
    {
        return SetSubtitleBottomTimer(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    public int SetSubtitleBottomTimerKeepID(string text, float entranceTime, float displayTime, float exitTime, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetSubtitleBottomTimer(text, entranceTime, displayTime, exitTime, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetSubtitleBottomTimer(text, entranceTime, displayTime, exitTime, IdType.keepIdButDeleteOnLoad);
        }
    }
    public int SetSubtitleBottomTimerKeepID(string text, float displayTime, bool dontDeleteOnLoad)
    {
        return SetSubtitleBottomTimerKeepID(text, defaultEntranceTime, displayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetSubtitleBottomTimerKeepID(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetSubtitleBottomTimerKeepID(text, entranceTime, displayTime, exitTime, false);
    }
    public int SetSubtitleBottomTimerKeepID(string text, float displayTime)
    {
        return SetSubtitleBottomTimerKeepID(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    //文字のみを引数にとる
    public int SetSubtitleBottomTimer(string text)
    {
        return SetSubtitleBottomTimer(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime);
    }

    public int SetSubtitleBottomTimerKeepID(string text, bool dontDeleteOnLoad)
    {
        return SetSubtitleBottomTimerKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetSubtitleBottomTimerKeepID(string text)
    {
        return SetSubtitleBottomTimerKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, false);
    }

    /// <summary>
    /// メッセージを設定します
    ///      そのメッセージはTopLeftTextに表示されます
    ///      MemoTopLeft関係メッセージ設定の基本関数です
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <param name="idType"></param>
    /// <returns></returns>
    public int SetMemoTopLeft(string text, float entranceTime, float displayTime, float exitTime, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Fade;
        param.exitAnimation = MessageExit.Fade;
        param.entranceTime = entranceTime;
        param.displayTime = displayTime;
        param.exitTime = exitTime;
        param.idType = idType;
        return Set(text, MessageType.TopLeft, MessageMode.Timer, param);
    }

    //文字と時間を引数にとる
    /// <summary>
    /// 画面右上のMessageを設定します
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <returns></returns>
    public int SetMemoTopLeft(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetMemoTopLeft(text, entranceTime, displayTime, exitTime, IdType.Normal);
    }
    public int SetMemoTopLeft(string text, float displayTime)
    {
        return SetMemoTopLeft(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    public int SetMemoTopLeftKeepID(string text, float entranceTime, float displayTime, float exitTime, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetMemoTopLeft(text, entranceTime, displayTime, exitTime, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetMemoTopLeft(text, entranceTime, displayTime, exitTime, IdType.keepIdButDeleteOnLoad);
        }
    }
    public int SetMemoTopLeftKeepID(string text, float displayTime, bool dontDeleteOnLoad)
    {
        return SetMemoTopLeftKeepID(text, defaultEntranceTime, displayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetMemoTopLeftKeepID(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetMemoTopLeftKeepID(text, entranceTime, displayTime, exitTime, false);
    }
    public int SetMemoTopLeftKeepID(string text, float displayTime)
    {
        return SetMemoTopLeftKeepID(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    //文字を引数にとる
    public int SetMemoTopLeft(string text)
    {
        return SetMemoTopLeft(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime);
    }

    public int SetMemoTopLeftKeepID(string text, bool dontDeleteOnLoad)
    {
        return SetMemoTopLeftKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetMemoTopLeftKeepID(string text)
    {
        return SetMemoTopLeftKeepID(text, false);
    }

    /// <summary>
    /// 
    ///     メッセージを設定します
    ///     そのメッセージはBottomRightTextに表示されます
    ///      MemoBottomRight関係メッセージ設定の基本関数です
    /// </summary>
    /// <param name="text"></param>
    /// <param name="entranceTime"></param>
    /// <param name="displayTime"></param>
    /// <param name="exitTime"></param>
    /// <param name="idType"></param>
    /// <returns></returns>
    public int SetMemoBottomRight(string text, float entranceTime, float displayTime, float exitTime, IdType idType)
    {
        Param param = new Param();
        param.entranceAnimation = MessageEntrance.Fade;
        param.exitAnimation = MessageExit.Fade;
        param.entranceTime = entranceTime;
        param.displayTime = displayTime;
        param.exitTime = exitTime;
        param.idType = idType;
        return Set(text, MessageType.BottomRight, MessageMode.Timer, param);
    }

    //文字と時間を引数にとる
    public int SetMemoBottomRight(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetMemoBottomRight(text, entranceTime, displayTime, exitTime, IdType.Normal);
    }
    public int SetMemoBottomRight(string text, float displayTime)
    {
        return SetMemoBottomRight(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    public int SetMemoBottomRightKeepID(string text, float entranceTime, float displayTime, float exitTime, bool dontDeleteOnLoad)
    {
        if (dontDeleteOnLoad)
        {
            return SetMemoBottomRight(text, entranceTime, displayTime, exitTime, IdType.DontDeleteOnLoad);
        }
        else
        {
            return SetMemoBottomRight(text, entranceTime, displayTime, exitTime, IdType.keepIdButDeleteOnLoad);
        }
    }
    public int SetMemoBottomRightKeepID(string text, float displayTime, bool dontDeleteOnLoad)
    {
        return SetMemoBottomRightKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetMemoBottomRightKeepID(string text, float entranceTime, float displayTime, float exitTime)
    {
        return SetMemoBottomRightKeepID(text, entranceTime, displayTime, exitTime, false);
    }
    public int SetMemoBottomRightKeepID(string text, float displayTime)
    {
        return SetMemoBottomRightKeepID(text, defaultEntranceTime, displayTime, defaultExitTime);
    }

    //文字を引数にとる
    public int SetMemoBottomRight(string text)
    {
        return SetMemoBottomRight(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime);
    }

    public int SetMemoBottomRightKeepID(string text, bool dontDeleteOnLoad)
    {
        return SetMemoBottomRightKeepID(text, defaultEntranceTime, defaultDisplayTime, defaultExitTime, dontDeleteOnLoad);
    }

    public int SetMemoBottomRightKeepID(string text)
    {
        return SetMemoBottomRightKeepID(text, false);
    }

    //
    //関数: Show, ShowDontOverride, ShowAlert, ShowAlertDontOverride
    //  説明:
    //      messageを開始する
    //
    /// <summary>
    /// Messageを表示する
    /// </summary>
    /// <param name="id"></param>
    public void Show(int id)
    {
        if (!CheckID(id))
        {
            return;
        }

        ParamPrivate paramPrivate = messageList[id].paramPrivate;

        paramPrivate.isPlaying = true;
        paramPrivate.messageStartTime = Time.unscaledTime;
        paramPrivate.messageState = MessageState.Start;
        paramPrivate.transparency = 0.0f;

        return;
    }

    public void ShowDontOverride(int id)
    {
        if (!CheckID(id))
        {
            return;
        }

        ParamPrivate paramPrivate = messageList[id].paramPrivate;
        if (!paramPrivate.isPlaying)
        {
            Show(id);
        }
    }

    /// <summary>
    /// 画面中央に文字を表示します
    /// </summary>
    /// <param name="text"></param>
    public void ShowAlert(string text)
    {
        int id;
        id = SetAlert(text);
        Show(id);
    }

    public void ShowAlert(string text, float entranceTime, float displayTime, float exitTime)
    {
        int id;
        id = SetAlert(text, entranceTime, displayTime, exitTime);
        Show(id);
    }
    public void ShowAlert(string text, float displayTime)
    {
        int id;
        id = SetAlert(text, defaultEntranceTime, displayTime, defaultExitTime);
        Show(id);
    }

    public void ShowSubtitleTopTimer(string text, float entranceTime, float displayTime, float exitTime)
    {
        int id;
        id = SetSubtitleTopTimer(text, entranceTime, displayTime, exitTime);
        Show(id);
    }
    public void ShowSubtitleTopTimer(string text, float displayTime)
    {
        int id;
        id = SetSubtitleTopTimer(text, defaultEntranceTime, displayTime, defaultExitTime);
        Show(id);
    }

    public void ShowSubtitleTopTimer(string text)
    {
        int id;
        id = SetSubtitleTopTimer(text);
        Show(id);
    }

    public void ShowSubtitleBottomTimer(string text, float entranceTime, float displayTime, float exitTime)
    {
        int id;
        id = SetSubtitleBottomTimer(text, entranceTime, displayTime, exitTime);
        Show(id);
    }
    public void ShowSubtitleBottomTimer(string text, float displayTime)
    {
        int id;
        id = SetSubtitleBottomTimer(text, defaultEntranceTime, displayTime, defaultExitTime);
        Show(id);
    }

    public void ShowSubtitleBottomTimer(string text)
    {
        int id;
        id = SetSubtitleBottomTimer(text);
        Show(id);
    }

    /// <summary>
    /// 画面左上に文字を表示します
    /// </summary>
    /// <param name="text"></param>
    public void ShowMemoTopLeft(string text)
    {
        int id;
        id = SetMemoTopLeft(text);
        Show(id);
    }

    public void ShowMemoTopLeft(string text, float entranceTime, float displayTime, float exitTime)
    {
        int id;
        id = SetMemoTopLeft(text, entranceTime, displayTime, exitTime);
        Show(id);
    }
    public void ShowMemoTopLeft(string text, float displayTime)
    {
        int id;
        id = SetMemoTopLeft(text, defaultEntranceTime, displayTime, defaultExitTime);
        Show(id);
    }

    /// <summary>
    /// 画面右下に文字を表示します
    /// </summary>
    /// <param name="text"></param>
    public void ShowMemoBottomRight(string text)
    {
        int id;
        id = SetMemoBottomRight(text);
        Show(id);
    }

    public void ShowMemoBottomRight(string text, float entranceTime, float displayTime, float exitTime)
    {
        int id;
        id = SetMemoBottomRight(text, entranceTime, displayTime, exitTime);
        Show(id);
    }
    public void ShowMemoBottomRight(string text, float displayTime)
    {
        int id;
        id = SetMemoBottomRight(text, defaultEntranceTime, displayTime, defaultExitTime);
        Show(id);
    }

    //
    //関数: Exit
    //  説明:
    //      messageを終了する
    //
    /// <summary>
    /// Messageを終了する
    /// </summary>
    /// <param name="id"></param>
    public void Exit(int id)
    {
        if (!CheckID(id))
        {
            return;
        }

        ParamPrivate paramPrivate = messageList[id].paramPrivate;
        if (paramPrivate.isPlaying)
        {
            paramPrivate.messageState = MessageState.ToEnd;
        }

    }


    //
    //関数: public bool CheckMessageID(int id)
    //  説明:
    //      指定したメッセージIDが使用されているか確認します
    public bool CheckID(int id)
    {
        if (messageList.Count < id + 1 || id < 0)
        {
            return false;
        }

        if (!messageList[id].paramPrivate.activated)
        {
            return false;
        }
        return true;
    }

    public void Clear()
    {

        foreach (MessageListElement message in messageList)
        {

            message.paramPrivate.activated = false;
        }

    }

}
