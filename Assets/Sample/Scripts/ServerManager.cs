using MLAPI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking.Match;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

namespace UTJ.MLAPISample
{
    // ホスト接続した際に、MLAPIからのコールバックを管理して切断時等の処理をします
    public class ServerManager : MonoBehaviour
    {
        public Button stopButton;
        public GameObject configureObject;

        public GameObject serverInfoRoot;
        public Text serverInfoText;

        private MLAPI.Transports.Tasks.SocketTasks socketTasks;
        private ConnectInfo cachedConnectInfo;

        public GameLift gameLift;

        public void SetSocketTasks(MLAPI.Transports.Tasks.SocketTasks tasks)
        {
            socketTasks = tasks;
        }

        public void Setup(ConnectInfo connectInfo, string localIp)
        {
            this.cachedConnectInfo = connectInfo;
            // サーバーとして起動したときのコールバック設定
            MLAPI.NetworkingManager.Singleton.OnServerStarted += this.OnStartServer;
            // クライアントが接続された時のコールバック設定
            MLAPI.NetworkingManager.Singleton.OnClientConnectedCallback += this.OnClientConnect;
            // クライアントが切断された時のコールバック設定
            MLAPI.NetworkingManager.Singleton.OnClientDisconnectCallback += this.OnClientDisconnect;

            //クライアントの接続を承認する？
            NetworkingManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;

            if (connectInfo.useRelay)
            {
                MLAPI.Transports.UNET.RelayTransport.OnRemoteEndpointReported += OnRelayEndPointReported;
                this.serverInfoRoot.SetActive(true);
                this.serverInfoText.text = "Relayサーバーに繋がっていません";
            }
            else
            {
                var stringBuilder = new System.Text.StringBuilder(256);
                this.serverInfoRoot.SetActive(true);
                stringBuilder.Append("サーバー接続情報\n").
                    Append("接続先IP:").Append(localIp).Append("\n").
                    Append("Port番号:").Append(connectInfo.port);
                this.serverInfoText.text = stringBuilder.ToString();
            }
            // transportの初期化
            MLAPI.NetworkingManager.Singleton.NetworkConfig.NetworkTransport.Init();
        }

        private void OnRelayEndPointReported(System.Net.IPEndPoint endPoint)
        {
            var stringBuilder = new System.Text.StringBuilder(256);
            this.serverInfoRoot.SetActive(true);
            stringBuilder.Append("サーバー接続情報\n").
                Append("接続先IP:").Append(endPoint.Address.ToString()).Append("\n").
                Append("Port番号:").Append(endPoint.Port).Append("\n").
                Append("Relay IP:").Append(cachedConnectInfo.relayIpAddr).Append("\n").
                Append("Relay Port:").Append(cachedConnectInfo.relayPort);
            this.serverInfoText.text = stringBuilder.ToString();
        }

        private void RemoveCallBack()
        {
            // サーバーとして起動したときのコールバック設定
            MLAPI.NetworkingManager.Singleton.OnServerStarted -= this.OnStartServer;
            // クライアントが接続された時のコールバック設定
            MLAPI.NetworkingManager.Singleton.OnClientConnectedCallback -= this.OnClientConnect;
            // クライアントが切断された時のコールバック設定
            MLAPI.NetworkingManager.Singleton.OnClientDisconnectCallback -= this.OnClientDisconnect;
            NetworkingManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            if (this.cachedConnectInfo.useRelay)
            {
                MLAPI.Transports.UNET.RelayTransport.OnRemoteEndpointReported -= OnRelayEndPointReported;
            }
        }

        //クライアントの接続を承認する？
        private void ApprovalCheck(byte[] connectionData, ulong clientId, MLAPI.NetworkingManager.ConnectionApprovedDelegate callback)
        {
            Debug.Log("ApprovalCheck connectionData:" + System.Text.Encoding.ASCII.GetString(connectionData));
            //Your logic here
            // ここにあなたの論理
            bool approve = true;
            //bool createPlayerObject = true;


#if SERVER
            if (gameLift == null)
            {
                Debug.Log("GameLift object is null!");
            }
            else
            {
                //GameLiftにプレイヤーセッションを問い合わせる
                approve = gameLift.ConnectPlayer((int)clientId, System.Text.Encoding.ASCII.GetString(connectionData));
                if (!approve) { DisconnectPlayer(clientId); }
            }
#endif


            // The prefab hash. Use null to use the default player prefab
            // プレハブハッシュ。デフォルトのプレーヤープレハブを使用するには、nullを使用します
            // If using this hash, replace "MyPrefabHashGenerator" with the name of a prefab added to the NetworkedPrefabs field of your NetworkingManager object in the scene
            // このハッシュを使用する場合は、「MyPrefabHashGenerator」を、シーン内のNetworkingManagerオブジェ
            // クトのNetworkedPrefabsフィールドに追加されたプレハブの名前に置き換えます。
            //ulong? prefabHash = SpawnManager.GetPrefabHashFromGenerator("MyPrefabHashGenerator");

            //If approve is true, the connection gets added. If it's false. The client gets disconnected
            // 承認がtrueの場合、接続が追加されます。それが間違っている場合。クライアントが切断さ
            // れます
            //callback(createPlayerObject, prefabHash, approve, positionToSpawnAt, rotationToSpawnWith);
            callback(false, null, approve, null, null);
        }

        // クライアントが接続してきたときの処理
        private void OnClientConnect(ulong clientId)
        {
            Debug.Log("Connect Client " + clientId);
            // クライアント用にキャラクターを生成します
            SpawnCharacter(clientId);
        }

        // クライアントが切断した時の処理
        private void OnClientDisconnect(ulong clientId)
        {
            Debug.Log("Disconnect Client " + clientId);
            DisconnectPlayer(clientId);
        }

        //GameLiftからプレイヤーセッションを削除
        private void DisconnectPlayer(ulong clientId)
        {
#if SERVER
            gameLift.DisconnectPlayer((int)clientId);   //プレイヤーセッションを解放
#endif
        }

        // サーバー開始時の処理
        private void OnStartServer()
        {
            Debug.Log("Start Server");
            var clientId = MLAPI.NetworkingManager.Singleton.ServerClientId;
            // hostならば生成します
            if (MLAPI.NetworkingManager.Singleton.IsHost)
            {
                SpawnCharacter(clientId);
            }

            configureObject.SetActive(false);
            stopButton.GetComponentInChildren<Text>().text = "Stop Host";
            stopButton.onClick.AddListener(OnClickDisconnectButton);
            stopButton.gameObject.SetActive(true);
        }

        // 切断ボタンが呼び出された時の処理
        private void OnClickDisconnectButton()
        {
            MLAPI.NetworkingManager.Singleton.StopHost();

#if SERVER
            //GameLiftのゲームセッションを削除
            if (gameLift != null && gameLift.gameliftStatus)
            {
                Debug.Log("TerminateGameSession");
                gameLift.TerminateGameSession(true);
            }
#endif

            this.RemoveCallBack();

            this.configureObject.SetActive(true);
            this.stopButton.gameObject.SetActive(false);
            this.serverInfoRoot.SetActive(false);
        }

        // ネットワーク同期するキャラクターオブジェクトを生成します
        private void SpawnCharacter(ulong clientId)
        {
            var netMgr = MLAPI.NetworkingManager.Singleton;
            var networkedPrefab = netMgr.NetworkConfig.NetworkedPrefabs[0];
            var randomPosition = new Vector3(Random.Range(-7, 7), 5.0f, Random.Range(-7, 7));
            var gmo = GameObject.Instantiate(networkedPrefab.Prefab, randomPosition, Quaternion.identity);
            var netObject = gmo.GetComponent<NetworkedObject>();
            // このNetworkオブジェクトをクライアントでもSpawnさせます
            netObject.SpawnWithOwnership(clientId);
        }


    }
}