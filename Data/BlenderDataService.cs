namespace FaraBlenderManual.Data;

public class BlenderDataService
{
    public List<BlenderOperation> GetBlenderOperations()
    {
        return new List<BlenderOperation>
        {
            new BlenderOperation
            {
                Title = "基本的な移動・回転・拡縮",
                Description = "オブジェクトの基本的な変形操作を学習します。",
                Difficulty = "初級",
                Steps = new List<string>
                {
                    "オブジェクトを選択する",
                    "Gキーで移動モード、Rキーで回転モード、Sキーで拡縮モードに切り替える",
                    "X、Y、Zキーで軸を固定する",
                    "数値を入力して正確な変形を行う",
                    "Enterキーで確定、Escキーでキャンセル"
                }
            },
            new BlenderOperation
            {
                Title = "メッシュの編集モード",
                Description = "頂点、辺、面を編集してメッシュを変形する方法を学習します。",
                Difficulty = "初級",
                Steps = new List<string>
                {
                    "オブジェクトを選択してTabキーで編集モードに入る",
                    "1キーで頂点選択、2キーで辺選択、3キーで面選択に切り替える",
                    "Alt+左クリックで辺ループ、面ループを選択",
                    "Eキーで押し出し、Iキーで面の内側押し出し",
                    "Ctrl+Rでループカット"
                }
            },
            new BlenderOperation
            {
                Title = "マテリアルとテクスチャ",
                Description = "オブジェクトにマテリアルを適用してテクスチャを設定する方法を学習します。",
                Difficulty = "中級",
                Steps = new List<string>
                {
                    "シェーディングワークスペースに切り替える",
                    "新しいマテリアルを作成する",
                    "Principled BSDFノードでマテリアルの設定を行う",
                    "Image Textureノードでテクスチャを追加",
                    "ColorRampやMixノードで色を調整"
                }
            },
            new BlenderOperation
            {
                Title = "リギングとアーマチュア",
                Description = "ボーンを使ってキャラクターをアニメーション可能にする方法を学習します。",
                Difficulty = "上級",
                Steps = new List<string>
                {
                    "Shift+Aでアーマチュアを追加",
                    "編集モードでボーンを配置・調整",
                    "メッシュとアーマチュアを選択してCtrl+Pで親子関係を設定",
                    "自動ウェイトまたは手動でウェイトペイント",
                    "ポーズモードでボーンを動かして確認"
                }
            }
        };
    }

    public List<BlenderShortcut> GetBlenderShortcuts()
    {
        return new List<BlenderShortcut>
        {
            // 一般
            new BlenderShortcut { Key = "Ctrl+N", Function = "新規ファイル", Category = "一般", Description = "新しいBlenderファイルを作成" },
            new BlenderShortcut { Key = "Ctrl+O", Function = "開く", Category = "一般", Description = "ファイルを開く" },
            new BlenderShortcut { Key = "Ctrl+S", Function = "保存", Category = "一般", Description = "現在のファイルを保存" },
            new BlenderShortcut { Key = "Ctrl+Z", Function = "元に戻す", Category = "一般", Description = "直前の操作を取り消し" },
            new BlenderShortcut { Key = "Ctrl+Shift+Z", Function = "やり直し", Category = "一般", Description = "取り消した操作をやり直し" },
            
            // 選択
            new BlenderShortcut { Key = "A", Function = "全選択", Category = "選択", Description = "すべてのオブジェクトを選択" },
            new BlenderShortcut { Key = "Alt+A", Function = "選択解除", Category = "選択", Description = "すべての選択を解除" },
            new BlenderShortcut { Key = "Ctrl+I", Function = "選択反転", Category = "選択", Description = "選択状態を反転" },
            new BlenderShortcut { Key = "B", Function = "矩形選択", Category = "選択", Description = "矩形範囲で選択" },
            new BlenderShortcut { Key = "C", Function = "円形選択", Category = "選択", Description = "円形範囲で選択" },
            
            // モデリング
            new BlenderShortcut { Key = "Tab", Function = "編集モード切り替え", Category = "モデリング", Description = "オブジェクトモードと編集モード切り替え" },
            new BlenderShortcut { Key = "E", Function = "押し出し", Category = "モデリング", Description = "選択した要素を押し出し" },
            new BlenderShortcut { Key = "I", Function = "面の内側押し出し", Category = "モデリング", Description = "面を内側に押し出し" },
            new BlenderShortcut { Key = "Ctrl+R", Function = "ループカット", Category = "モデリング", Description = "エッジループを作成" },
            new BlenderShortcut { Key = "K", Function = "ナイフツール", Category = "モデリング", Description = "メッシュを切断" },
            
            // 編集
            new BlenderShortcut { Key = "G", Function = "移動", Category = "編集", Description = "選択したオブジェクトを移動" },
            new BlenderShortcut { Key = "R", Function = "回転", Category = "編集", Description = "選択したオブジェクトを回転" },
            new BlenderShortcut { Key = "S", Function = "拡縮", Category = "編集", Description = "選択したオブジェクトを拡縮" },
            new BlenderShortcut { Key = "X", Function = "削除", Category = "編集", Description = "選択した要素を削除" },
            new BlenderShortcut { Key = "Shift+D", Function = "複製", Category = "編集", Description = "選択したオブジェクトを複製" },
            
            // 表示
            new BlenderShortcut { Key = "Home", Function = "全体表示", Category = "表示", Description = "すべてのオブジェクトが見えるように表示" },
            new BlenderShortcut { Key = ".", Function = "選択オブジェクトにフォーカス", Category = "表示", Description = "選択オブジェクトを中心に表示" },
            new BlenderShortcut { Key = "H", Function = "非表示", Category = "表示", Description = "選択したオブジェクトを非表示" },
            new BlenderShortcut { Key = "Alt+H", Function = "非表示解除", Category = "表示", Description = "すべてのオブジェクトを表示" },
            new BlenderShortcut { Key = "Z", Function = "シェーディング切り替え", Category = "表示", Description = "表示モードの切り替え" }
        };
    }

    public List<BlenderPlugin> GetBlenderPlugins()
    {
        return new List<BlenderPlugin>
        {
            new BlenderPlugin
            {
                Name = "Hard Ops",
                Description = "ハードサーフェスモデリングを効率化するプロフェッショナル向けアドオン",
                Type = "モデリング",
                Price = "有料 ($20)",
                Features = new List<string>
                {
                    "高度なブール演算",
                    "ハードサーフェス用ツール群",
                    "自動トポロジー",
                    "カスタムシェーダー"
                },
                Url = "https://hardops.gumroad.com/"
            },
            new BlenderPlugin
            {
                Name = "Cats Blender Plugin",
                Description = "VRChatアバター作成に特化したアドオン。アバターの最適化やセットアップが簡単に",
                Type = "ゲーム開発",
                Price = "無料",
                Features = new List<string>
                {
                    "アバター最適化",
                    "ボーン構造修正",
                    "表情作成支援",
                    "Unity連携"
                },
                Url = "https://github.com/absolute-quantum/cats-blender-plugin"
            },
            new BlenderPlugin
            {
                Name = "Animation Nodes",
                Description = "ノードベースでアニメーションを作成できるパワフルなアドオン",
                Type = "アニメーション",
                Price = "無料",
                Features = new List<string>
                {
                    "ノードベースアニメーション",
                    "プロシージャルアニメーション",
                    "パーティクルシステム連携",
                    "数学的アニメーション"
                },
                Url = "https://github.com/JacquesLucke/animation_nodes"
            },
            new BlenderPlugin
            {
                Name = "Cycles X",
                Description = "次世代レンダーエンジン。より高速で高品質なレンダリングが可能",
                Type = "レンダリング",
                Price = "内蔵（Blender 3.0+）",
                Features = new List<string>
                {
                    "高速レンダリング",
                    "GPU最適化",
                    "リアルタイムプレビュー",
                    "AI画像補完"
                },
                Url = ""
            },
            new BlenderPlugin
            {
                Name = "Extra Objects",
                Description = "デフォルトでは含まれていない様々なオブジェクトを追加",
                Type = "ユーティリティ",
                Price = "内蔵",
                Features = new List<string>
                {
                    "建築用オブジェクト",
                    "機械部品",
                    "装飾オブジェクト",
                    "数学的形状"
                },
                Url = ""
            },
            new BlenderPlugin
            {
                Name = "Node Wrangler",
                Description = "マテリアルノード編集を効率化するアドオン",
                Type = "ユーティリティ",
                Price = "内蔵",
                Features = new List<string>
                {
                    "ノード自動接続",
                    "プレビュー機能",
                    "テクスチャセット読み込み",
                    "ショートカット拡張"
                },
                Url = ""
            }
        };
    }

    public List<ClothingTutorial> GetClothingTutorials()
    {
        return new List<ClothingTutorial>
        {
            new ClothingTutorial
            {
                Title = "準備：アバターモデルのインポート",
                Description = "VRChatアバター用のベースモデルをBlenderにインポートして基本設定を行います。",
                Steps = new List<string>
                {
                    "File > Import > FBX からアバターファイルを選択",
                    "インポート設定でスケールとアーマチュアを確認",
                    "アバターが正常に表示されることを確認",
                    "必要に応じてマテリアルを再設定"
                },
                Tips = new List<string>
                {
                    "インポート時にスケールが1.0になっているか確認",
                    "アーマチュアが含まれているか確認"
                }
            },
            new ClothingTutorial
            {
                Title = "服の基本形状作成",
                Description = "アバターに合わせて服の基本的な形状を作成します。",
                Steps = new List<string>
                {
                    "Shift+A > Mesh > Cube で基本形状を追加",
                    "Tab キーで編集モードに入る",
                    "S キーで服のサイズに合わせて拡縮",
                    "E キーで押し出しを使って形状を調整",
                    "Loop Cut（Ctrl+R）で詳���な形状を作成"
                },
                Tips = new List<string>
                {
                    "アバターの体型に合わせて少し大きめに作成",
                    "対称性を保つためにMirrorモディファイアを使用"
                }
            },
            new ClothingTutorial
            {
                Title = "UV展開とテクスチャマッピング",
                Description = "作成した服のUV展開を行い、テクスチャを適用できるように準備します。",
                Steps = new List<string>
                {
                    "UV Editingワークスペースに切り替え",
                    "編集モードで全面を選択（A キー）",
                    "U キー > Unwrap でUV展開",
                    "UV > Export UV Layout でUVマップを書き出し",
                    "外部ソフトでテクスチャを作成"
                },
                Tips = new List<string>
                {
                    "シームマーク（Ctrl+E > Mark Seam）で展開位置を調整",
                    "UV密度を均等に保つことでテクスチャ品質を向上"
                }
            },
            new ClothingTutorial
            {
                Title = "ウェイトペイント",
                Description = "服がアバターの動きに合わせて変形するようにウェイトを設定します。",
                Steps = new List<string>
                {
                    "服のオブジェクトを選択後、アバターのアーマチュアを選択",
                    "Ctrl+P > With Automatic Weights で自動ウェイト",
                    "Weight Paintモードでウェイトを調整",
                    "各ボーンの影響範囲を手動で修正",
                    "ポーズモードでテスト"
                },
                Tips = new List<string>
                {
                    "重要な関節部分は特に注意してウェイトを調整",
                    "ブラシの強度を調整して自然な変形を目指す"
                }
            },
            new ClothingTutorial
            {
                Title = "Unityでの最終設定",
                Description = "完成した服をUnityにインポートしてVRChatで使用できるように設定します。",
                Steps = new List<string>
                {
                    "File > Export > FBX で服をエクスポート",
                    "Unityプロジェクトにファイルをインポート",
                    "アバターディスクリプターでOutfitを設定",
                    "マテリアル設定を調整",
                    "アップロード前にテスト"
                },
                Tips = new List<string>
                {
                    "エクスポート時にスケールとアーマチュア設定を確認",
                    "VRChatの制限（ポリゴン数、マテリアル数）に注意"
                }
            }
        };
    }
}
