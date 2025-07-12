# BlueprintUnlockManager
This plugin manages blueprint unlocks in Rust by limiting the number of learners per item and enforcing a queue system to rotate blueprint ownership.

# 作った人のコメント
BPドロップ式のRUSTサーバは一部のアイテムのBPがでると終わり的なものですよね。
そこでBPの奪い合いという新しいゲーム性を追加するプラグインを作りました！
さぁ、みなさん。BPを奪い合いましょう。

# 【完全版】使い方
1.いわゆるBPドロップ式のサーバ（リサーチベンチとかテックツリーが使えなくてBPがドロップするサーバ）を作ります。EzBpDropOnlyとかなんでもOK

2.このプラグインを入れます。

3.レッツRUST！！

# BPの所有数制限を設定するには？
oxide/config/BlueprintUnlockManager.json
このファイルでBPの所有者の制限をしたいアイテム(ShortName)と最大所有者数(MaxLearners)登録する。
設定を変更した場合は、必ず再起動してね！

例：AKは2人、MP5は3人
```
{
  "Items": [
    {
      "MaxLearners": 2,
      "ShortName": "rifle.ak"
    },
    {
      "MaxLearners": 3,
      "ShortName": "smg.mp5"
    }
  ]
}
```


# 弱点
あくまでBPを覚えてる人に対して影響のあるプラグインなので、クラフター(Industrial Crafter)とかで作られたら制限できません。
なので、クラフター(Industrial Crafter)はサーバから除外してください。
