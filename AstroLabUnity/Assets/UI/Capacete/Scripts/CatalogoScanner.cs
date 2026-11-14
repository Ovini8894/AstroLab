using System.Text.RegularExpressions;
using UnityEngine;

namespace AstroLab.Capacete
{
    public static class CatalogoScanner
    {
        public struct Dados
        {
            public string titulo, descricao, charada, categoria;
            public GameObject objeto;
            public AlvoExploracao cadastro;
        }

        public static Dados Descrever(Collider collider)
        {
            var cadastro=collider.GetComponentInParent<AlvoExploracao>();
            if(cadastro)return new Dados { objeto=cadastro.gameObject,cadastro=cadastro,titulo=cadastro.titulo,descricao=string.IsNullOrWhiteSpace(cadastro.descricao)?"Objeto registrado para exploração na base lunar.":cadastro.descricao,charada=cadastro.charada,categoria=cadastro.categoria };
            var nome=collider.name;
            var dados=new Dados { objeto=collider.gameObject,titulo=NomeLegivel(nome),categoria="SCANNER / AMBIENTE LUNAR",charada="",descricao="Elemento do ambiente identificado como “"+NomeLegivel(nome)+"”. Este objeto ainda não possui uma descrição específica cadastrada no catálogo." };
            var n=nome.ToLowerInvariant();
            if(n.Contains("painel") || n.Contains("solar")) {dados.titulo="Painel solar";dados.descricao="Superfície de um sistema de geração de energia solar. Os módulos convertem a luz recebida em energia elétrica para os equipamentos da base.";}
            else if(n.Contains("bateria")){dados.titulo="Banco de baterias";dados.descricao="Equipamento de armazenamento de energia elétrica. Trabalha em conjunto com a geração e a distribuição de energia da base.";}
            else if(n.Contains("antena")){dados.titulo="Antena de comunicação";dados.descricao="Estrutura usada para transmitir ou receber sinais de comunicação. Sua orientação e suas conexões fazem parte do funcionamento do sistema.";}
            else if(n.Contains("rocha") || n.Contains("pedra") || n.Contains("rock")){dados.titulo="Rocha lunar";dados.descricao="Formação rochosa representada no ambiente lunar. Sua composição específica exige dados de análise; a aparência, sozinha, não permite determinar os minerais presentes.";}
            else if(n.Contains("regolito") || n.Contains("terreno") || n.Contains("solo") || n.Contains("ground")){dados.titulo="Superfície lunar";dados.descricao="Camada superficial do terreno, representada por poeira e fragmentos de rocha. O scanner identifica a superfície; não realiza uma análise química do material.";}
            else if(n.Contains("porta")){dados.titulo="Porta da base";dados.descricao="Elemento de acesso entre áreas da estação. Pode fazer parte de uma eclusa ou de uma divisão interna; consulte os sistemas da base para conhecer seu estado operacional.";}
            else if(n.Contains("tanque") || n.Contains("reservatorio")){dados.titulo="Reservatório";dados.descricao="Recipiente de armazenamento do sistema da base. Consulte a identificação do equipamento para confirmar o conteúdo e a função.";}
            else if(n.Contains("cano") || n.Contains("tubo")){dados.titulo="Tubulação";dados.descricao="Trecho de conexão entre equipamentos ou reservatórios. A identificação do sistema conectado informa o fluido transportado e a sua função.";}
            else if(n.Contains("escada") || n.Contains("rampa")){dados.titulo="Estrutura de acesso";dados.descricao="Estrutura que permite a circulação entre o terreno e as plataformas da base lunar.";}
            else if(n.Contains("casco") || n.Contains("parede") || n.Contains("piso") || n.Contains("teto")){dados.titulo="Estrutura da base";dados.descricao="Parte da estrutura do módulo lunar. A função exata e o estado de integridade dependem do sistema e dos dados de manutenção cadastrados.";}
            return dados;
        }

        static string NomeLegivel(string nome)
        {
            nome=Regex.Replace(nome.Replace("REPAIR_","").Replace("_"," "),@"\.\d+$","");
            return string.IsNullOrWhiteSpace(nome)?"Objeto não catalogado":nome;
        }
    }
}
