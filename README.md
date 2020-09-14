# Kalmia
Kalmiaはモンテカルロ木探索ベースのリバーシ思考エンジンです。
今現在、思考エンジンそのものは実装しておらず、ビットボード及びGUIとエンジンとの通信プロトコルを実装中です。

# RVTP
RVTP(ReVersi Text Protocol)とはリバーシの思考エンジンとGUIプログラム間の自作の通信プロトコルです。 オープンソースの囲碁思考エンジンGNU Go("https://www.gnu.org/software/gnugo/")で実装された通信プロトコルであるGTP(Go Text Protocol)の仕様を元に実装しています。
あくまでも自作プロトコルなので、自分で開発した複数の思考エンジンとの対戦を円滑に行うために開発しています。

RVTPの仕様については Kalmia/Kalmia/ReversiTextProtocol/RTP.txt　に記載しておりますが、明確に固まっているわけではないドラフト仕様です。
