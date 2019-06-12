
import Vue, { VNode } from 'vue';
import vuescroll from 'vue-scroll'


import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import HeaderLayout from "./HeaderLayout";
import HeroLayout from './HeroLayout';
import { WDynItem } from '../pages/FrontPage';
import { VBtn, VDialog, VCard, VCardTitle, VCardText, VDivider, VCardActions, VSpacer, VContainer, VLayout, VFlex, VTextField } from 'vuetify-tsx';

Vue.use(vuescroll)

export interface DefaultLayoutOptions {

}

const color1 = [248, 127, 46];
const color2 = [255, 255, 255];
const diff = [color2[0] - color1[0], color2[1] - color1[1], color2[2] - color1[2]]
const scale = 0.3;


export class AppSetting extends Vue {

    keyValue!:string;
    value!: string;

    constructor(keyValue: string , value: string ) { super({ data: { keyValue, value } }) }

     
}

@Component
export class KeyValueComponent extends tsx.Component<{ keyValue?: string }, { onNewData }>
{

    @Prop()
    keyValue!: string;

    value = { key: this.keyValue, value: "" };

    @Watch("value", { deep: true })
    onValueChange(val, old) {
        console.log(this.value);
        this.$emit("newData", [this.value.key, this.value.value])
    }

    render() {
        return (
            <VLayout>
                <v-flex xs6> <VTextField label="Key" v-model={this.value.key} /></v-flex>
                <v-flex xs6> <VTextField label="Value" v-model={this.value.value} /></v-flex>
            </VLayout>
            );
    }
}

@Component
export class DeployPopup extends tsx.Component<{ functionName: string, initialAppSettings?: any }>{

    resourceApi = "https://management.dotnetdevops.org";

    storageResourceId: string | null = null;

    @Prop()
    functionName!: string;

    dialog = false;
    closeDialog() {
        this.dialog = false;
    }
   
    addApplicationSetting() {
        this.appSettings.push(new AppSetting("",""));
        //this.settings.push(<KeyValueComponent value={{ key: "", value:"" }} />)
    }

    @Prop({
        type: Array,
        default: () => [],
    })
    initialAppSettings!: Array<AppSetting>;

    appSettings = this.initialAppSettings


   

    get href() {

        let encodedUri = encodeURIComponent(this.resourceApi + `/providers/DotNetDevOps.AzureTemplates/templates/azure-function?function=${this.functionName}&storageResourceId=${encodeURIComponent(this.storageResourceId||"")}&${this.appSettings.map(kv => `appsetting_${kv.keyValue}=${encodeURIComponent(kv.value)}`).join('&')}`);

        let url = `https://portal.azure.com/#create/Microsoft.Template/uri/${encodedUri}`;
            console.log(url);
            return url;
         
    }
    render() {

        let settings = this.appSettings && this.appSettings.map(v => (<KeyValueComponent keyValue={v.keyValue} onNewData={(e) => { console.log(e);[v.keyValue, v.value] = e }} />));
        
        //@ts-ignore
        return (<VDialog dark v-model={this.dialog} persistent width="700" scopedSlots={{
            activator: (props) => (
                <VBtn onClick={props.on.click} class="azuredeploy">
                    <span class="pr-2">Deploy to</span>
                    <svg xmlnsSvg="http://www.w3.org/2000/svg" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 52.91666 15.244921" version="1.1" >
                        <defs id="defs2" />

                        <g transform="translate(677.9313,-313.85407)">
                            <g transform="matrix(0.03978217,0,0,0.03978217,-658.51488,317.36166)" id="layer1-1" >
                                <path id="path21" d="m -259.18627,274.57516 c 63.5635,-11.22863 116.06332,-20.52032 116.66629,-20.64819 l 1.09632,-0.2325 -60.01021,-71.38026 c -33.00559,-39.25915 -60.01018,-71.53249 -60.01018,-71.71853 0,-0.35223 61.96561,-170.992127 62.31396,-171.599207 0.11647,-0.20297 42.28595,72.60148 102.221586,176.482797 56.11284,97.25555 102.3751556,177.4431 102.8051496,178.19452 l 0.78184,1.36624 -190.7173956,-0.0246 -190.71736,-0.0245 115.57,-20.4157 z m 731.37889,-17.26291 c -29.03093,-1.86128 -45.91544,-18.39214 -50.38974,-49.33422 -1.19021,-8.2311 -1.19476,-8.44466 -1.31656,-61.88078 l -0.11789,-51.717227 h 12.84834 12.8483 l 0.10118,50.023887 c 0.0912,45.08514 0.14842,50.37131 0.57946,53.54292 1.74935,12.87168 5.23201,21.5276 11.16124,27.74067 4.74528,4.97242 10.30038,7.8839 17.99011,9.42882 3.62783,0.72884 13.94813,0.7303 17.25569,0.003 7.79588,-1.7156 14.0442,-5.10811 19.55617,-10.61799 6.28249,-6.28012 10.93015,-15.1903 13.17899,-25.26595 l 0.75793,-3.39586 0.0844,-50.44723 0.0844,-50.447217 h 13.11732 13.11735 v 79.304447 79.30444 h -12.98222 -12.98222 v -12.5824 c 0,-8.54933 -0.0937,-12.55118 -0.29222,-12.485 -0.16069,0.0536 -0.82744,1.07357 -1.48166,2.26667 -4.47373,8.15896 -11.92745,15.62029 -20.09501,20.1155 -9.79024,5.38827 -19.60858,7.30422 -33.02333,6.44412 z m 294.66636,-0.12816 c -10.24795,-0.7703 -21.03846,-4.29359 -29.85796,-9.74908 -18.58873,-11.49853 -29.58761,-30.45184 -32.80805,-56.53499 -1.11441,-9.02575 -1.24835,-21.14964 -0.3184,-28.81907 2.07786,-17.13682 8.81185,-33.95976 18.40106,-45.96982 2.45824,-3.07883 8.03495,-8.65553 11.11365,-11.11361 8.31379,-6.637877 18.03843,-11.279617 28.36334,-13.538327 6.01579,-1.31603 16.60968,-1.93394 23.00111,-1.34159 16.05576,1.48805 30.77105,9.04765 40.77267,20.945887 10.1615,12.08847 15.74681,28.98006 16.46007,49.77987 0.11179,3.25966 0.13889,8.97466 0.0603,12.7 l -0.143,6.77333 -56.23278,0.0712 -56.23278,0.0712 v 2.5094 c 0,7.63915 1.85722,16.33991 5.06795,23.74265 2.76877,6.38373 7.53485,13.35462 11.43714,16.72802 8.00131,6.91687 17.79386,11.05701 28.50936,12.05334 3.97318,0.3694 14.09889,-0.0195 18.62666,-0.7152 12.91802,-1.98524 25.19946,-7.052 35.11745,-14.48783 1.16688,-0.87486 2.28357,-1.69223 2.48152,-1.81635 0.28968,-0.18164 0.35814,2.10408 0.35081,11.71222 l -0.009,11.93792 -2.65743,1.64355 c -11.21971,6.93911 -24.07305,11.39481 -37.68211,13.06277 -4.06135,0.49775 -18.96443,0.71949 -23.82142,0.35441 z m 48.93919,-100.68528 c 0,-12.79609 -5.39245,-27.01096 -13.02337,-34.3305 -5.44587,-5.22367 -12.02921,-8.41603 -19.85552,-9.62822 -3.70264,-0.5735 -11.50631,-0.35262 -15.41004,0.43617 -8.25234,1.66748 -15.07811,5.29536 -21.03154,11.17821 -6.26561,6.19133 -10.96323,13.71122 -13.91756,22.27909 -1.06234,3.08091 -2.30488,8.13901 -2.69056,10.95257 l -0.18376,1.34056 h 43.05616 43.05619 z M 53.010852,253.20058 c 0.06587,-0.19403 19.161194,-50.3586 42.434086,-111.47682 l 42.314342,-111.124037 13.59583,-8.5e-4 13.5958,-8.4e-4 1.12805,2.89278 c 3.4472,8.84008 84.71032,219.821587 84.71032,219.931197 0,0.0722 -6.50875,0.13039 -14.46389,0.12923 l -14.46389,-0.003 -11.71222,-31.18282 -11.71222,-31.18281 -47.15885,-5.7e-4 -47.15886,-5.6e-4 -0.40982,1.05833 c -0.22538,0.58208 -5.229376,14.61335 -11.120031,31.18062 l -10.710221,30.12225 -14.494087,0.005 c -11.4727,0.004 -14.469109,-0.0686 -14.374339,-0.34784 z M 189.82928,167.38571 c 0,-0.0494 -7.88187,-21.41719 -17.51527,-47.48389 -18.02243,-48.766297 -19.03061,-51.700447 -20.45454,-59.529577 -0.66771,-3.67122 -1.00556,-3.77324 -1.40188,-0.42333 -0.2833,2.39448 -1.51167,7.75245 -2.45866,10.72445 -0.46988,1.47461 -8.58577,23.74972 -18.03533,49.500237 -9.44954,25.75052 -17.18099,46.92777 -17.18099,47.06055 0,0.13279 17.33551,0.24143 38.52334,0.24143 21.18783,0 38.52333,-0.0404 38.52333,-0.0899 z m 69.70889,82.10639 v -4.06127 l 46.98461,-64.58929 46.98461,-64.5893 -42.53961,-0.14111 -42.53961,-0.14111 -0.0739,-10.795 -0.0739,-10.794997 h 61.52781 61.5278 v 3.64606 3.646057 l -46.99,64.94153 c -25.8445,35.71785 -46.99,65.00162 -46.99,65.07506 0,0.0734 20.8915,0.13352 46.42555,0.13352 h 46.42556 v 10.86555 10.86556 h -65.33447 -65.33445 z m 344.78149,3.8731 c -0.10349,-0.10346 -0.18816,-35.91746 -0.18816,-79.58666 V 94.380023 h 12.84112 12.84111 v 16.368887 c 0,9.00289 0.10947,16.36889 0.2433,16.36889 0.13383,0 0.75881,-1.47368 1.38884,-3.27484 2.88711,-8.2536 7.89393,-15.96572 14.34443,-22.09509 5.81095,-5.521657 12.45235,-8.823637 20.28898,-10.087287 2.20134,-0.35497 4.064,-0.42274 8.46667,-0.30806 5.52924,0.14402 8.42001,0.54819 11.78278,1.64738 l 1.05833,0.34594 v 13.328937 13.32893 l -3.03389,-1.51868 c -5.3592,-2.68267 -10.64714,-3.74429 -17.00389,-3.41375 -4.13707,0.21513 -6.85252,0.74636 -10.16,1.98766 -6.80519,2.55399 -12.32518,7.06198 -16.18228,13.2155 -5.58374,8.90817 -9.56612,20.35873 -10.7407,30.88287 -0.21079,1.88864 -0.33508,17.27019 -0.38459,47.59327 l -0.0731,44.80278 H 617.1582 c -6.95771,0 -12.73506,-0.0847 -12.83852,-0.18816 z M -488.0685,252.80762 c 0,-0.1005 28.27652,-49.18632 62.83671,-109.07964 l 62.8367,-108.896937 73.22891,-61.45385 c 40.2759,-33.79961 73.33737,-61.49516 73.46992,-61.54567 0.13255,-0.0505 -0.39727,1.28299 -1.17738,2.96333 -0.78011,1.68034 -36.56244,78.4296699 -79.51629,170.55406 l -78.09789,167.498887 -56.79034,0.0712 c -31.23468,0.0392 -56.79034,-0.011 -56.79034,-0.11142 z" style="fill:#0089d6;fill-opacity:1;stroke-width:0.28222221" />
                            </g>
                        </g>
                    </svg>
                </VBtn>
            )
        }}>

            <VCard>
                <VCardTitle> <span class="headline">Deploy to Azure</span></VCardTitle>
                <VCardText  class="grey darken-2 text-xs-center">
                    <VContainer class="grid-list-md">
                        <VLayout wrap>
                            <v-flex xs12>
                                <form>
                                    <VTextField label="AzureWebJobsStorage ResourceId" v-model={this.storageResourceId} />       
                                </form>
                            </v-flex>
                        </VLayout>
                    </VContainer>
                </VCardText>
                <v-card-text dark style="  position: relative">
                    <VLayout wrap>
                        <v-flex xs12>
                            <h1>AppSettings</h1>
                            </v-flex>
                    </VLayout>
                    <VContainer class="grid-list-md">
                       
                        <VLayout wrap>
                            <v-flex xs12>
                                <form>
                                    {settings}  
                                </form>
                            </v-flex>
                        </VLayout>
                    </VContainer>
                </v-card-text>
                <v-card-text class="grey darken-2" style="  position: relative">
                    <v-fab-transition>
                        <v-btn onClick={this.addApplicationSetting}
                            color="pink"
                            dark
                            absolute
                            top
                            right
                            fab
                        >
                            <v-icon>add</v-icon>
                        </v-btn>
                    </v-fab-transition>
                </v-card-text>
                
                <VCardActions class="grey darken-2">
                    <VSpacer />
                    <v-btn color="blue darken-1" flat onClick={this.closeDialog}>Close</v-btn>
                    <VBtn target="_blank" href={this.href} flat class="azuredeploy">
                        <span class="pr-2">Deploy</span>                       
                    </VBtn>

                </VCardActions>

            </VCard>
        </VDialog>);
    }
}
// target="_blank" href={`https://portal.azure.com/#create/Microsoft.Template/uri/${encodeURI(this.resourceApi + `/providers/DotNetDevOps.AzureTemplates/templates/azure-function?function=${this.functionName}`)}`}
@Component
export default class DefaultLayout extends tsx.Component<DefaultLayoutOptions>{

 

    backgroundColor = "rgb(248, 127, 46)";
    transform = "scale3d(1, 1, 1)";

    onScroll() {
        

        let element = document.scrollingElement as Element;
        let p = element.scrollTop / ((element.scrollHeight - element.clientHeight));
        p = isNaN(p) ? 1 : p;



        this.backgroundColor = `rgb(${color1[0] + diff[0] * p},${color1[1] + diff[1] * p},${color1[2] + diff[2] * p})`;
        this.transform = `scale3d(${1 - scale * p}, ${1 - scale * p}, ${1 - scale * p})`;

        if (element.scrollHeight - element.scrollTop === element.clientHeight) {
            console.log('scrolled');
            document.body.classList.add("scrolled");
        } else {
            document.body.classList.remove("scrolled");
        }


        return p;
    }

    mounted() {
        document.addEventListener("scroll", (event) => {
            let p = this.onScroll();

        });
    }



    render() {
        return (
            <div>
                <HeaderLayout backgroundColor={this.backgroundColor}>
                    <template slot="links">
                        <a href="/" class="link-nav w-nav-link w--current">Info</a>
                        <a href="/dashboard/" class="link-nav w-nav-link">Blog</a>
                    </template>
                </HeaderLayout>
                <HeroLayout transform={this.transform} backgroundColor={this.backgroundColor} title="DotNET DevOps" subtitle="Deliver software faster" />

                <router-view>
                    <WDynItem video="devops1.mp4" title="LetsEncrypt" number="01"  >                        
                        <template slot="info">
                            <DeployPopup functionName="DotNetDevOps.LetsEncrypt" />                           
                        </template>
                    </WDynItem>
                    <WDynItem title="DotNET DevOps Routr" number="02">
                        <p class="reader">
                          <b>DotNET DevOps Routr</b> is a consumption based reverse proxy build on .net core that allow you to quickly configure, deploy and manage your routing of microservices across several azure services.
                                        </p>
                        <p class="reader">
                            The reverse proxy allows easy nginx inspired configuration of routing to azure functions, blob storage and other azure services.
                        </p>
                        <template slot="info">
                            <DeployPopup functionName="DotNETDevOps.FrontDoor.RouterFunction" initialAppSettings={[new AppSetting("RemoteConfiguration", "")]} />
                        </template>
                        
                    </WDynItem>
                </router-view>
            </div>
        );
    }
}
