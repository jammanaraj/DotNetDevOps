
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';


export interface AppOptions {

}

@Component
export default class App extends tsx.Component<AppOptions>{

    render() {
        return (
            <router-view key={this.$route.fullPath}></router-view>
        );
    }
}
